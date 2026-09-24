using Microsoft.EntityFrameworkCore;
using PersonalTechBrief.Application.Briefs;
using PersonalTechBrief.Application.Interactions;
using PersonalTechBrief.Domain.Analysis;
using PersonalTechBrief.Domain.Briefs;
using PersonalTechBrief.Domain.Interactions;
using PersonalTechBrief.Infrastructure.Persistence;

namespace PersonalTechBrief.Infrastructure.Briefs;

/// <summary>
/// SQL-backed brief persistence (FR-011/012/014, data model §11). Historical briefs are write-once;
/// candidate loading excludes updates already represented in a prior completed brief (the approved
/// simpler already-briefed rule, §20).
/// </summary>
public sealed class SqlBriefRepository(PersonalTechBriefDbContext dbContext) : IBriefRepository
{
    public async Task<DateTime?> GetLatestCompletedBriefGeneratedAtUtcAsync(CancellationToken cancellationToken)
    {
        var latest = await dbContext.Briefs.AsNoTracking()
            .Where(brief => brief.Status == BriefStatus.Completed)
            .OrderByDescending(brief => brief.GeneratedAtUtc)
            .Select(brief => (DateTime?)brief.GeneratedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return latest is { } value ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : null;
    }

    public async Task AddAsync(Brief brief, CancellationToken cancellationToken)
    {
        await dbContext.Briefs.AddAsync(brief, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Brief?> GetForGenerationAsync(Guid briefId, CancellationToken cancellationToken) =>
        dbContext.Briefs.FirstOrDefaultAsync(brief => brief.Id == briefId, cancellationToken);

    public async Task<IReadOnlyList<BriefCandidate>> LoadCandidatesAsync(
        DateTime windowStartUtc,
        double threshold,
        CancellationToken cancellationToken)
    {
        var briefedUpdateIds =
            from item in dbContext.BriefItems
            join brief in dbContext.Briefs on item.BriefId equals brief.Id
            where brief.Status == BriefStatus.Completed
            select item.TechnologyUpdateId;

        var rows = await dbContext.TechnologyUpdates.AsNoTracking()
            .Where(update => update.Status == TechnologyUpdateStatus.Active)
            .Where(update => update.CurrentRelevanceScore >= threshold)
            .Where(update => update.LastObservedAtUtc >= windowStartUtc)
            .Where(update => !briefedUpdateIds.Contains(update.Id))
            .OrderByDescending(update => update.CurrentRelevanceScore)
            .ThenBy(update => update.Id)
            .Select(update => new
            {
                update.Id,
                update.CurrentRelevanceScore,
                update.LastObservedAtUtc,
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new BriefCandidate(
                row.Id,
                row.CurrentRelevanceScore,
                DateTime.SpecifyKind(row.LastObservedAtUtc, DateTimeKind.Utc),
                AlreadyBriefed: false))
            .ToList();
    }

    public async Task<BriefGenerationInput?> LoadGenerationInputAsync(
        Guid technologyUpdateId,
        CancellationToken cancellationToken)
    {
        var update = await dbContext.TechnologyUpdates.AsNoTracking()
            .Where(candidate => candidate.Id == technologyUpdateId)
            .Select(candidate => new { candidate.PrimaryTopic })
            .FirstOrDefaultAsync(cancellationToken);

        if (update is null)
        {
            return null;
        }

        var sources = await (
            from association in dbContext.TechnologyUpdateSources.AsNoTracking()
            where association.TechnologyUpdateId == technologyUpdateId
            join item in dbContext.SourceItems.AsNoTracking() on association.SourceItemId equals item.Id
            orderby item.PublishedAtUtc descending, item.Id
            select new
            {
                item.Id,
                item.Title,
                item.Excerpt,
                item.CanonicalUrl,
                item.OriginalUrl,
                item.PublishedAtUtc,
            }).ToListAsync(cancellationToken);

        var interests = await (
            from match in dbContext.UpdateInterestMatches.AsNoTracking()
            where match.TechnologyUpdateId == technologyUpdateId
            join interest in dbContext.Interests.AsNoTracking() on match.InterestId equals interest.Id
            where interest.IsActive
            orderby match.MatchStrength descending, interest.Id
            select new
            {
                interest.Id,
                interest.Name,
                interest.Priority,
            }).ToListAsync(cancellationToken);

        return new BriefGenerationInput(
            technologyUpdateId,
            update.PrimaryTopic,
            sources
                .Select(source => new BriefSourceInput(
                    source.Id,
                    source.Title,
                    source.Excerpt,
                    source.CanonicalUrl ?? source.OriginalUrl,
                    source.PublishedAtUtc is { } published ? DateTime.SpecifyKind(published, DateTimeKind.Utc) : null))
                .ToList(),
            interests
                .Select(interest => new BriefInterestInput(interest.Id, interest.Name, interest.Priority))
                .ToList());
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task MarkFailedAsync(Guid briefId, CancellationToken cancellationToken)
    {
        // Discard any staged item snapshots so a failed run never persists a partial brief.
        dbContext.ChangeTracker.Clear();

        var brief = await dbContext.Briefs.FirstOrDefaultAsync(candidate => candidate.Id == briefId, cancellationToken);
        if (brief is null || brief.Status != BriefStatus.Generating)
        {
            return;
        }

        brief.Fail();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> FailOrphanedGeneratingBriefsAsync(
        DateTime createdBeforeUtc,
        CancellationToken cancellationToken)
    {
        // Only sweep briefs that already existed when reconciliation started; a brief created by a
        // POST that races startup (GeneratedAtUtc >= the cutoff) is left for the drain loop.
        var orphans = await dbContext.Briefs
            .Where(brief => brief.Status == BriefStatus.Generating && brief.GeneratedAtUtc < createdBeforeUtc)
            .ToListAsync(cancellationToken);
        if (orphans.Count == 0)
        {
            return 0;
        }

        foreach (var orphan in orphans)
        {
            orphan.Fail();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return orphans.Count;
    }

    public Task<BriefReadModel?> GetCurrentAsync(CancellationToken cancellationToken) =>
        LoadReadModelAsync(
            dbContext.Briefs.AsNoTracking()
                .Where(brief => brief.Status == BriefStatus.Completed)
                .OrderByDescending(brief => brief.GeneratedAtUtc),
            cancellationToken);

    public Task<BriefReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        LoadReadModelAsync(
            dbContext.Briefs.AsNoTracking().Where(brief => brief.Id == id),
            cancellationToken);

    public async Task<BriefPage> ListAsync(string? cursor, int limit, CancellationToken cancellationToken)
    {
        var query = dbContext.Briefs.AsNoTracking().OrderByDescending(brief => brief.GeneratedAtUtc);

        IQueryable<Brief> ordered = query;
        if (TryDecodeCursor(cursor, out var cursorGeneratedAtUtc))
        {
            ordered = query.Where(brief => brief.GeneratedAtUtc < cursorGeneratedAtUtc);
        }

        var rows = await ordered
            .Take(limit + 1)
            .Select(brief => new BriefSummaryReadModel(
                brief.Id,
                brief.GeneratedAtUtc,
                brief.WindowStartUtc,
                brief.WindowEndUtc,
                brief.Status,
                brief.CandidateCount,
                brief.SelectedCount,
                brief.GenerationVersion,
                brief.CorrelationId))
            .ToListAsync(cancellationToken);

        string? nextCursor = null;
        if (rows.Count > limit)
        {
            var last = rows[limit - 1];
            nextCursor = EncodeCursor(last.GeneratedAtUtc);
            rows = rows.Take(limit).ToList();
        }

        var normalized = rows
            .Select(row => row with { GeneratedAtUtc = DateTime.SpecifyKind(row.GeneratedAtUtc, DateTimeKind.Utc) })
            .ToList();

        return new BriefPage(normalized, nextCursor);
    }

    private async Task<BriefReadModel?> LoadReadModelAsync(
        IQueryable<Brief> query,
        CancellationToken cancellationToken)
    {
        var brief = await query
            .Select(candidate => new BriefSummaryReadModel(
                candidate.Id,
                candidate.GeneratedAtUtc,
                candidate.WindowStartUtc,
                candidate.WindowEndUtc,
                candidate.Status,
                candidate.CandidateCount,
                candidate.SelectedCount,
                candidate.GenerationVersion,
                candidate.CorrelationId))
            .FirstOrDefaultAsync(cancellationToken);

        if (brief is null)
        {
            return null;
        }

        var summary = brief with { GeneratedAtUtc = DateTime.SpecifyKind(brief.GeneratedAtUtc, DateTimeKind.Utc) };

        var itemRows = await dbContext.BriefItems.AsNoTracking()
            .Where(item => item.BriefId == summary.Id)
            .OrderBy(item => item.Rank)
            .Select(item => new
            {
                item.Id,
                item.TechnologyUpdateId,
                item.Rank,
                item.TitleSnapshot,
                item.TopicSnapshot,
                item.SummarySnapshot,
                item.WhyRelevantSnapshot,
                item.RelevanceScoreSnapshot,
                item.GeneratedAtUtc,
                item.PromptVersion,
                item.ModelOrAlgorithmVersion,
                Sources = dbContext.BriefItemSources.AsNoTracking()
                    .Where(source => source.BriefItemId == item.Id)
                    .OrderBy(source => source.SourceItemId)
                    .Select(source => new BriefItemSourceReadModel(
                        source.SourceItemId,
                        source.TitleSnapshot,
                        source.UrlSnapshot,
                        source.PublishedAtUtc))
                    .ToList(),
                // Live interaction state for the item's update (does not touch the snapshot).
                LatestFeedback = dbContext.UserFeedback.AsNoTracking()
                    .Where(feedback => feedback.TechnologyUpdateId == item.TechnologyUpdateId)
                    .OrderByDescending(feedback => feedback.CreatedAtUtc)
                    .ThenByDescending(feedback => feedback.Id)
                    .Select(feedback => (FeedbackType?)feedback.FeedbackType)
                    .FirstOrDefault(),
                IsSaved = dbContext.SavedUpdates.AsNoTracking()
                    .Any(saved => saved.TechnologyUpdateId == item.TechnologyUpdateId),
            })
            .ToListAsync(cancellationToken);

        var items = itemRows
            .Select(item => new BriefItemReadModel(
                item.Id,
                item.TechnologyUpdateId,
                item.Rank,
                item.TitleSnapshot,
                item.TopicSnapshot,
                item.SummarySnapshot,
                item.WhyRelevantSnapshot,
                item.RelevanceScoreSnapshot,
                DateTime.SpecifyKind(item.GeneratedAtUtc, DateTimeKind.Utc),
                item.PromptVersion,
                item.ModelOrAlgorithmVersion,
                item.Sources
                    .Select(source => source with
                    {
                        PublishedAtUtc = source.PublishedAtUtc is { } published
                            ? DateTime.SpecifyKind(published, DateTimeKind.Utc)
                            : null,
                    })
                    .ToList(),
                item.LatestFeedback is { } feedbackType ? FeedbackValue.ToWireValue(feedbackType) : null,
                item.IsSaved))
            .ToList();

        return new BriefReadModel(summary, items);
    }

    private static string EncodeCursor(DateTime generatedAtUtc) =>
        Convert.ToBase64String(BitConverter.GetBytes(generatedAtUtc.Ticks));

    private static bool TryDecodeCursor(string? cursor, out DateTime generatedAtUtc)
    {
        generatedAtUtc = default;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(cursor);
            if (bytes.Length != sizeof(long))
            {
                return false;
            }

            generatedAtUtc = new DateTime(BitConverter.ToInt64(bytes), DateTimeKind.Unspecified);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
