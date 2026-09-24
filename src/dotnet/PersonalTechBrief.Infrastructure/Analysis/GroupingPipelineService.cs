using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Analysis;
using PersonalTechBrief.Application.Interests;
using PersonalTechBrief.Application.Messaging;
using PersonalTechBrief.Infrastructure.Intelligence;
using PersonalTechBrief.Infrastructure.Intelligence.Contracts;
using DomainInterestPriority = PersonalTechBrief.Domain.Interests.InterestPriority;

namespace PersonalTechBrief.Infrastructure.Analysis;

/// <summary>
/// Grouping/relevance application service (pipeline §13 stages 6–8). For one durable inbox receipt it
/// analyzes the item via the Intelligence API, bounds candidates, computes similarity, links or creates
/// a <see cref="Domain.Analysis.TechnologyUpdate"/>, scores it deterministically, persists, and marks the
/// receipt processed. It is idempotent under at-least-once redelivery: an already-handled item is a no-op.
/// Transient Python/SQL failures leave the receipt pending for a bounded retry; validation failures and an
/// exhausted retry budget are terminal so the loop cannot spin forever.
/// </summary>
public sealed class GroupingPipelineService(
    IGroupingRepository groupingRepository,
    IInterestRepository interestRepository,
    IIntelligenceApiClient intelligenceClient,
    IContentProcessingInboxStore inboxStore,
    IRelevanceScorer scorer,
    IOptions<GroupingPipelineOptions> options,
    TimeProvider timeProvider,
    ILogger<GroupingPipelineService> logger) : IGroupingPipelineService
{
    private const string DefaultPrimaryTopic = "general";
    private readonly GroupingPipelineOptions options = options.Value;

    public async Task<GroupingProcessingOutcome> ProcessAsync(
        SourceItemReadyEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var item = await groupingRepository.GetSourceItemAsync(envelope.SourceItemId, cancellationToken);
        if (item is null)
        {
            await inboxStore.MarkProcessedAsync(envelope.SourceItemId, cancellationToken);
            logger.LogWarning(
                "Completing receipt for missing source item {SourceItemId}; correlation {CorrelationId}.",
                envelope.SourceItemId,
                envelope.CorrelationId);
            return GroupingProcessingOutcome.SkippedItemMissing;
        }

        if (item.IsInTerminalState || item.IsAlreadyGrouped)
        {
            await inboxStore.MarkProcessedAsync(item.Id, cancellationToken);
            logger.LogInformation(
                "Source item {SourceItemId} already handled (status {Status}, grouped {Grouped}); receipt completed idempotently.",
                item.Id,
                item.ProcessingStatus,
                item.IsAlreadyGrouped);
            return GroupingProcessingOutcome.SkippedAlreadyHandled;
        }

        try
        {
            return await RunPipelineAsync(envelope, item, utcNow, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (IntelligenceApiException exception)
        {
            var terminal = IsTerminalIntelligenceFailure(exception.StatusCode);
            return await RecordFailureAsync(
                item,
                terminal ? "IntelligenceValidation" : "IntelligenceTransient",
                terminal,
                exception,
                cancellationToken);
        }
        catch (Exception exception)
        {
            // Persistence/transport faults are transient; the bounded-retry budget prevents an infinite loop.
            return await RecordFailureAsync(item, "GroupingTransient", terminal: false, exception, cancellationToken);
        }
    }

    private async Task<GroupingProcessingOutcome> RunPipelineAsync(
        SourceItemReadyEnvelope envelope,
        GroupingSourceItem item,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var activeInterests = await interestRepository.ListActiveAsync(cancellationToken);
        var priorityById = activeInterests.ToDictionary(interest => interest.Id, interest => interest.Priority);

        var analysis = await intelligenceClient.AnalyzeAsync(
            new AnalyzeRequest
            {
                CorrelationId = envelope.CorrelationId,
                Item = new AnalyzeItem
                {
                    SourceItemId = item.Id,
                    Title = item.Title,
                    Excerpt = item.Excerpt,
                    Content = null,
                    Language = null,
                },
                CandidateInterests = activeInterests
                    .Select(interest => new CandidateInterest
                    {
                        InterestId = interest.Id,
                        Name = interest.Name,
                        Priority = MapPriority(interest.Priority),
                    })
                    .ToList(),
            },
            cancellationToken);

        // Only matches against still-active interests, with a positive strength, count toward the group.
        var interestMatches = analysis.InterestMatches
            .Where(match => priorityById.ContainsKey(match.InterestId) && match.MatchStrength > 0d)
            .Select(match => new InterestMatchRecord(match.InterestId, match.MatchStrength))
            .ToList();

        var primaryTopic = analysis.Topics.FirstOrDefault(topic => !string.IsNullOrWhiteSpace(topic))?.Trim()
            ?? DefaultPrimaryTopic;
        var probeText = $"{item.Title}\n{primaryTopic}";

        var (mergeTargetId, similarityScore) = await ResolveGroupingDecisionAsync(
            envelope.CorrelationId, item, primaryTopic, analysis.Topics, probeText, utcNow, cancellationToken);

        var impact = MapImpact(analysis.Impact.Level);

        RelevanceScore ComputeScore(GroupScoringSignals signals)
        {
            var interestSignals = signals.GroupInterestMatches
                .Where(match => priorityById.ContainsKey(match.InterestId))
                .Select(match => new InterestMatchSignal(priorityById[match.InterestId], match.MatchStrength))
                .ToList();
            return scorer.Score(new RelevanceScoreRequest(
                interestSignals,
                impact,
                signals.NewestSupportingTimestampUtc,
                utcNow,
                signals.DistinctSupportingHostCount));
        }

        var result = await groupingRepository.CommitGroupingAsync(
            new CommitGroupingCommand(
                item.Id,
                mergeTargetId,
                similarityScore,
                item.Title,
                primaryTopic,
                item.ObservedAtUtc,
                interestMatches,
                utcNow),
            ComputeScore,
            cancellationToken);

        await inboxStore.MarkProcessedAsync(item.Id, cancellationToken);

        logger.LogInformation(
            "Grouped source item {SourceItemId} into update {TechnologyUpdateId} (created {Created}, score {Score:0.###}); correlation {CorrelationId}.",
            item.Id,
            result.TechnologyUpdateId,
            result.Created,
            result.RelevanceScore,
            envelope.CorrelationId);
        return GroupingProcessingOutcome.Grouped;
    }

    private async Task<(Guid? MergeTargetId, double? SimilarityScore)> ResolveGroupingDecisionAsync(
        Guid correlationId,
        GroupingSourceItem item,
        string primaryTopic,
        IReadOnlyList<string> topics,
        string probeText,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var windowStart = utcNow.AddHours(-options.GroupingWindowHours);
        var candidateTopics = topics
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Select(topic => topic.Trim())
            .Append(primaryTopic)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var candidates = await groupingRepository.FindCandidateUpdatesAsync(
            new GroupingCandidateQuery(windowStart, candidateTopics, options.MaxComparisons),
            cancellationToken);
        if (candidates.Count == 0)
        {
            return (null, null);
        }

        var similarity = await intelligenceClient.ComputeSimilarityAsync(
            new SimilarityRequest
            {
                CorrelationId = correlationId,
                Candidate = new SimilarityCandidate { Text = probeText },
                Representatives = candidates
                    .Select(candidate => new SimilarityRepresentative
                    {
                        UpdateId = candidate.TechnologyUpdateId,
                        Text = candidate.RepresentativeText,
                    })
                    .ToList(),
            },
            cancellationToken);

        var best = similarity.Results
            .OrderByDescending(result => result.Similarity)
            .ThenBy(result => result.UpdateId)
            .FirstOrDefault();

        if (best is not null && best.Similarity >= options.MergeThreshold)
        {
            return (best.UpdateId, best.Similarity);
        }

        return (null, null);
    }

    private async Task<GroupingProcessingOutcome> RecordFailureAsync(
        GroupingSourceItem item,
        string failureCode,
        bool terminal,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var escalate = terminal || item.FailureCount + 1 >= options.MaxProcessingAttempts;
        var failureCount = await groupingRepository.RecordProcessingFailureAsync(
            item.Id, failureCode, escalate, cancellationToken);

        if (escalate)
        {
            await inboxStore.MarkProcessedAsync(item.Id, cancellationToken);
            logger.LogError(
                exception,
                "Terminal failure {FailureCode} for source item {SourceItemId} after {FailureCount} attempt(s); receipt closed.",
                failureCode,
                item.Id,
                failureCount);
            return GroupingProcessingOutcome.FailedTerminal;
        }

        logger.LogWarning(
            exception,
            "Transient failure {FailureCode} for source item {SourceItemId} (attempt {FailureCount}); receipt left pending for retry.",
            failureCode,
            item.Id,
            failureCount);
        return GroupingProcessingOutcome.RetryScheduled;
    }

    private static bool IsTerminalIntelligenceFailure(HttpStatusCode? statusCode) =>
        statusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity;

    private static ImpactTier MapImpact(ImpactLevel level) => level switch
    {
        ImpactLevel.High => ImpactTier.High,
        ImpactLevel.Medium => ImpactTier.Medium,
        ImpactLevel.Low => ImpactTier.Low,
        _ => ImpactTier.None,
    };

    private static InterestPriority MapPriority(DomainInterestPriority priority) => priority switch
    {
        DomainInterestPriority.High => InterestPriority.High,
        DomainInterestPriority.Medium => InterestPriority.Medium,
        DomainInterestPriority.Low => InterestPriority.Low,
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unsupported interest priority."),
    };
}
