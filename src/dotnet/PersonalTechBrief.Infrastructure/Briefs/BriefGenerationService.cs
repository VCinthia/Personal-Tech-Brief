using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Analysis;
using PersonalTechBrief.Application.Briefs;
using PersonalTechBrief.Domain.Briefs;
using PersonalTechBrief.Infrastructure.Intelligence;
using PersonalTechBrief.Infrastructure.Intelligence.Contracts;
using DomainInterestPriority = PersonalTechBrief.Domain.Interests.InterestPriority;

namespace PersonalTechBrief.Infrastructure.Briefs;

/// <summary>
/// Orchestrates asynchronous brief generation (FR-011/018, UC-007/008, pipeline §13 stages 9-11). It
/// selects candidates deterministically, generates each in rank order via the Intelligence API with
/// bounded transient retry, persists an immutable snapshot on success, excludes a candidate on
/// persistent failure (never inventing or padding), and completes the brief with the generated items
/// (possibly fewer than selected, possibly zero). It never rewrites an existing brief.
/// </summary>
public sealed class BriefGenerationService(
    IBriefRepository repository,
    IBriefCandidateSelector selector,
    IIntelligenceApiClient intelligenceApiClient,
    IOptions<RelevanceScoringOptions> relevanceOptions,
    IOptions<BriefGenerationOptions> briefOptions,
    TimeProvider timeProvider,
    ILogger<BriefGenerationService> logger) : IBriefGenerationService
{
    private readonly RelevanceScoringOptions _relevanceOptions = relevanceOptions.Value;
    private readonly BriefGenerationOptions _briefOptions = briefOptions.Value;

    public async Task GenerateAsync(Guid briefId, CancellationToken cancellationToken)
    {
        var brief = await repository.GetForGenerationAsync(briefId, cancellationToken);
        if (brief is null)
        {
            logger.LogWarning("Brief {BriefId} was not found for generation; skipping.", briefId);
            return;
        }

        if (brief.Status != BriefStatus.Generating)
        {
            logger.LogInformation("Brief {BriefId} is already {Status}; generation is a no-op.", briefId, brief.Status);
            return;
        }

        try
        {
            var threshold = _relevanceOptions.SelectionThreshold;
            var candidates = await repository.LoadCandidatesAsync(brief.WindowStartUtc, threshold, cancellationToken);
            var selection = selector.Select(candidates, brief.WindowStartUtc, threshold, _briefOptions.MaxItems);

            var items = new List<BriefItem>();
            var rank = 0;
            foreach (var candidate in selection.Selected)
            {
                var input = await repository.LoadGenerationInputAsync(candidate.TechnologyUpdateId, cancellationToken);
                if (input is null || input.Sources.Count == 0)
                {
                    logger.LogWarning(
                        "Candidate {TechnologyUpdateId} for brief {BriefId} has no supporting sources; excluding it.",
                        candidate.TechnologyUpdateId,
                        briefId);
                    continue;
                }

                var response = await TryGenerateAsync(brief.CorrelationId, input, cancellationToken);
                if (response is null)
                {
                    // Persistent generation failure: exclude the candidate (do not invent, do not pad). It is
                    // not marked briefed, so it stays eligible for a future brief.
                    continue;
                }

                rank++;
                var sources = input.Sources
                    .Select(source => new BriefItemSourceSnapshot(
                        source.SourceItemId,
                        source.Title,
                        source.SourceUrl,
                        source.PublishedAtUtc))
                    .ToList();

                items.Add(BriefItem.Create(
                    brief.Id,
                    candidate.TechnologyUpdateId,
                    rank,
                    response.Title,
                    input.PrimaryTopic,
                    response.Summary,
                    response.WhyRelevant,
                    candidate.RelevanceScore,
                    timeProvider.GetUtcNow().UtcDateTime,
                    response.GenerationVersion,
                    response.GenerationVersion,
                    sources));
            }

            brief.Complete(selection.CandidateCount, items);
            await repository.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Brief {BriefId} completed with {SelectedCount} of {CandidateCount} candidate(s).",
                briefId,
                items.Count,
                selection.CandidateCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Brief {BriefId} generation failed; marking it failed.", briefId);
            await repository.MarkFailedAsync(briefId, CancellationToken.None);
        }
    }

    private async Task<GenerateResponse?> TryGenerateAsync(
        Guid correlationId,
        BriefGenerationInput input,
        CancellationToken cancellationToken)
    {
        var request = BuildRequest(correlationId, input);
        var maxAttempts = Math.Max(1, _briefOptions.MaxGenerationAttempts);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await intelligenceApiClient.GenerateAsync(request, cancellationToken);
            }
            catch (IntelligenceApiException exception) when (IsTransient(exception) && attempt < maxAttempts)
            {
                logger.LogWarning(
                    "Transient generation failure for candidate {TechnologyUpdateId} (attempt {Attempt}); retrying.",
                    input.TechnologyUpdateId,
                    attempt);
                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }
            catch (IntelligenceApiException exception)
            {
                logger.LogWarning(
                    "Persistent generation failure for candidate {TechnologyUpdateId} (status {StatusCode}); excluding it.",
                    input.TechnologyUpdateId,
                    exception.StatusCode);
                return null;
            }
        }
    }

    // Domain maxima are wider than the frozen generate contract, so every bounded field is clamped and
    // every collection is capped to the contract limits here. Otherwise a candidate with a long real
    // title/topic/interest name would draw a 400 (non-transient), be excluded, and — never marked
    // briefed — fail on every future brief. This mirrors the contract's "source text bounded/truncated
    // before the provider call".
    private static GenerateRequest BuildRequest(Guid correlationId, BriefGenerationInput input) => new()
    {
        CorrelationId = correlationId,
        Update = new GenerateUpdate
        {
            TechnologyUpdateId = input.TechnologyUpdateId,
            PrimaryTopic = Clamp(input.PrimaryTopic, GenerateContractLimits.PrimaryTopicMaxLength)!,
            Sources = input.Sources
                .Take(GenerateContractLimits.MaxSources)
                .Select(source => new GenerateSource
                {
                    SourceItemId = source.SourceItemId,
                    Title = Clamp(source.Title, GenerateContractLimits.SourceTitleMaxLength)!,
                    Excerpt = Clamp(source.Excerpt, GenerateContractLimits.SourceExcerptMaxLength),
                    SourceUrl = source.SourceUrl,
                    PublishedAtUtc = source.PublishedAtUtc,
                })
                .ToList(),
        },
        MatchedInterests = input.Interests
            .Take(GenerateContractLimits.MaxMatchedInterests)
            .Select(interest => new GenerateMatchedInterest
            {
                InterestId = interest.InterestId,
                Name = Clamp(interest.Name, GenerateContractLimits.InterestNameMaxLength)!,
                Priority = MapPriority(interest.Priority),
            })
            .ToList(),
    };

    private static string? Clamp(string? value, int maxLength)
    {
        if (value is null)
        {
            return null;
        }

        return value.Length > maxLength ? value[..maxLength] : value;
    }

    private static InterestPriority MapPriority(DomainInterestPriority priority) => priority switch
    {
        DomainInterestPriority.High => InterestPriority.High,
        DomainInterestPriority.Medium => InterestPriority.Medium,
        DomainInterestPriority.Low => InterestPriority.Low,
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown interest priority."),
    };

    private static bool IsTransient(IntelligenceApiException exception) =>
        exception.StatusCode is null // client-side timeout or transport failure
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private async Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var baseDelay = _briefOptions.RetryBaseDelayMilliseconds;
        if (baseDelay <= 0)
        {
            return;
        }

        var exponential = baseDelay * Math.Pow(2, attempt - 1);
        var jitter = Random.Shared.Next(0, baseDelay + 1);
        await Task.Delay(TimeSpan.FromMilliseconds(exponential + jitter), cancellationToken);
    }
}
