using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PersonalTechBrief.Application.Analysis;
using PersonalTechBrief.Domain.Analysis;
using PersonalTechBrief.Infrastructure.Persistence;

namespace PersonalTechBrief.Infrastructure.Analysis;

/// <summary>
/// SQL-backed grouping and relevance persistence (FR-007/008/009/010/017). The link/create/score
/// decision commits in one serializable transaction and every write is idempotent on the source
/// item id, so at-least-once redelivery never creates a duplicate update or association.
/// </summary>
public sealed class SqlGroupingRepository(PersonalTechBriefDbContext dbContext) : IGroupingRepository
{
    public async Task<GroupingSourceItem?> GetSourceItemAsync(Guid sourceItemId, CancellationToken cancellationToken)
    {
        var row = await dbContext.SourceItems.AsNoTracking()
            .Where(item => item.Id == sourceItemId)
            .Select(item => new
            {
                item.Id,
                item.SourceId,
                item.Title,
                item.Excerpt,
                item.PublishedAtUtc,
                item.RetrievedAtUtc,
                item.ProcessingStatus,
                item.FailureCount,
                FeedUrl = dbContext.Sources
                    .Where(source => (Guid?)source.Id == item.SourceId)
                    .Select(source => source.FeedUrl)
                    .FirstOrDefault(),
                ExistingTechnologyUpdateId = dbContext.TechnologyUpdateSources
                    .Where(association => association.SourceItemId == item.Id)
                    .Select(association => (Guid?)association.TechnologyUpdateId)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new GroupingSourceItem(
            row.Id,
            row.SourceId,
            row.Title,
            row.Excerpt,
            row.PublishedAtUtc is { } published ? DateTime.SpecifyKind(published, DateTimeKind.Utc) : null,
            DateTime.SpecifyKind(row.RetrievedAtUtc, DateTimeKind.Utc),
            row.ProcessingStatus,
            row.FailureCount,
            ExtractHost(row.FeedUrl),
            row.ExistingTechnologyUpdateId);
    }

    public async Task<IReadOnlyList<GroupingCandidate>> FindCandidateUpdatesAsync(
        GroupingCandidateQuery query,
        CancellationToken cancellationToken)
    {
        if (query.MaxComparisons < 1)
        {
            return [];
        }

        var candidates = dbContext.TechnologyUpdates.AsNoTracking()
            .Where(update => update.LastObservedAtUtc >= query.ObservedWindowStartUtc);

        if (query.Topics.Count > 0)
        {
            candidates = candidates.Where(update => query.Topics.Contains(update.PrimaryTopic));
        }

        var rows = await candidates
            .OrderByDescending(update => update.LastObservedAtUtc)
            .ThenBy(update => update.Id)
            .Take(query.MaxComparisons)
            .Select(update => new { update.Id, update.RepresentativeTitle, update.PrimaryTopic })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new GroupingCandidate(row.Id, BuildRepresentativeText(row.RepresentativeTitle, row.PrimaryTopic)))
            .ToList();
    }

    public async Task<GroupingCommitResult> CommitGroupingAsync(
        CommitGroupingCommand command,
        Func<GroupScoringSignals, RelevanceScore> computeScore,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var alreadyLinkedId = await dbContext.TechnologyUpdateSources.AsNoTracking()
                .Where(association => association.SourceItemId == command.SourceItemId)
                .Select(association => (Guid?)association.TechnologyUpdateId)
                .FirstOrDefaultAsync(cancellationToken);
            if (alreadyLinkedId is { } linkedId)
            {
                var currentScore = await dbContext.TechnologyUpdates.AsNoTracking()
                    .Where(update => update.Id == linkedId)
                    .Select(update => update.CurrentRelevanceScore)
                    .FirstAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new GroupingCommitResult(linkedId, Created: false, currentScore);
            }

            TechnologyUpdate update;
            bool created;
            if (command.MergeTargetTechnologyUpdateId is { } targetId)
            {
                update = await dbContext.TechnologyUpdates.FirstAsync(candidate => candidate.Id == targetId, cancellationToken);
                update.RegisterSupportingObservation(command.ObservedAtUtc, command.UtcNow);
                created = false;
            }
            else
            {
                update = TechnologyUpdate.Create(command.RepresentativeTitle, command.PrimaryTopic, command.ObservedAtUtc);
                dbContext.TechnologyUpdates.Add(update);
                created = true;
            }

            dbContext.TechnologyUpdateSources.Add(
                TechnologyUpdateSource.Link(update.Id, command.SourceItemId, command.SimilarityScore, command.UtcNow));

            await UpsertInterestMatchesAsync(update.Id, command.InterestMatches, command.UtcNow, cancellationToken);

            // Persist the association and matches so the group-signal queries observe them.
            await dbContext.SaveChangesAsync(cancellationToken);

            var signals = await BuildScoringSignalsAsync(update.Id, cancellationToken);
            var score = computeScore(signals);
            update.SetRelevanceScore(score.Total, command.UtcNow);

            var sourceItem = await dbContext.SourceItems.FirstAsync(item => item.Id == command.SourceItemId, cancellationToken);
            sourceItem.MarkProcessed();

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new GroupingCommitResult(update.Id, created, score.Total);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            // A concurrent delivery for the same item won the unique pair; treat as an idempotent no-op.
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();

            var winnerId = await dbContext.TechnologyUpdateSources.AsNoTracking()
                .Where(association => association.SourceItemId == command.SourceItemId)
                .Select(association => (Guid?)association.TechnologyUpdateId)
                .FirstOrDefaultAsync(cancellationToken);
            if (winnerId is not { } winner)
            {
                // The violation was not the source-item pair; surface it for bounded retry.
                throw;
            }

            var winnerScore = await dbContext.TechnologyUpdates.AsNoTracking()
                .Where(update => update.Id == winner)
                .Select(update => update.CurrentRelevanceScore)
                .FirstAsync(cancellationToken);
            return new GroupingCommitResult(winner, Created: false, winnerScore);
        }
        catch
        {
            // The transaction rolls back on dispose; also drop any staged/mutated entities so a later
            // failure-recording read on this DbContext observes committed state, not the rolled-back
            // in-memory mutations (e.g. a premature SourceItem.MarkProcessed()).
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<int> RecordProcessingFailureAsync(
        Guid sourceItemId,
        string failureCode,
        bool terminal,
        CancellationToken cancellationToken)
    {
        // Discard any entities a failed grouping commit left tracked (including a premature
        // SourceItem.MarkProcessed()), so the failure is recorded against the committed,
        // rolled-back state and the bounded-retry FailureCount actually increments.
        dbContext.ChangeTracker.Clear();

        var sourceItem = await dbContext.SourceItems.FirstOrDefaultAsync(item => item.Id == sourceItemId, cancellationToken);
        if (sourceItem is null)
        {
            return 0;
        }

        sourceItem.RecordProcessingFailure(terminal, failureCode);
        await dbContext.SaveChangesAsync(cancellationToken);
        return sourceItem.FailureCount;
    }

    private async Task UpsertInterestMatchesAsync(
        Guid technologyUpdateId,
        IReadOnlyList<InterestMatchRecord> matches,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (matches.Count == 0)
        {
            return;
        }

        var existing = await dbContext.UpdateInterestMatches
            .Where(match => match.TechnologyUpdateId == technologyUpdateId)
            .ToListAsync(cancellationToken);

        foreach (var incoming in matches)
        {
            var match = existing.Find(candidate => candidate.InterestId == incoming.InterestId);
            if (match is null)
            {
                dbContext.UpdateInterestMatches.Add(
                    UpdateInterestMatch.Create(technologyUpdateId, incoming.InterestId, incoming.MatchStrength, utcNow));
            }
            else
            {
                match.Reinforce(incoming.MatchStrength, utcNow);
            }
        }
    }

    private async Task<GroupScoringSignals> BuildScoringSignalsAsync(
        Guid technologyUpdateId,
        CancellationToken cancellationToken)
    {
        var supportingRows = await (
            from association in dbContext.TechnologyUpdateSources.AsNoTracking()
            where association.TechnologyUpdateId == technologyUpdateId
            join item in dbContext.SourceItems.AsNoTracking() on association.SourceItemId equals item.Id
            select new
            {
                item.PublishedAtUtc,
                item.RetrievedAtUtc,
                FeedUrl = dbContext.Sources
                    .Where(source => (Guid?)source.Id == item.SourceId)
                    .Select(source => source.FeedUrl)
                    .FirstOrDefault(),
            }).ToListAsync(cancellationToken);

        var newest = supportingRows
            .Select(row => DateTime.SpecifyKind(row.PublishedAtUtc ?? row.RetrievedAtUtc, DateTimeKind.Utc))
            .DefaultIfEmpty()
            .Max();

        var distinctHosts = supportingRows
            .Select(row => ExtractHost(row.FeedUrl))
            .Where(host => host is not null)
            .Distinct(StringComparer.Ordinal)
            .Count();

        var groupMatches = await dbContext.UpdateInterestMatches.AsNoTracking()
            .Where(match => match.TechnologyUpdateId == technologyUpdateId)
            .Select(match => new InterestMatchRecord(match.InterestId, match.MatchStrength))
            .ToListAsync(cancellationToken);

        return new GroupScoringSignals(newest, distinctHosts, groupMatches);
    }

    private static string BuildRepresentativeText(string representativeTitle, string primaryTopic) =>
        $"{representativeTitle}\n{primaryTopic}";

    private static string? ExtractHost(string? feedUrl)
    {
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            return null;
        }

        return Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri)
            ? uri.Host.ToLowerInvariant()
            : null;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
