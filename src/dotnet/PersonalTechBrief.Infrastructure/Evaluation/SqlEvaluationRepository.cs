using Microsoft.EntityFrameworkCore;
using PersonalTechBrief.Application.Evaluation;
using PersonalTechBrief.Domain.Briefs;
using PersonalTechBrief.Infrastructure.Persistence;

namespace PersonalTechBrief.Infrastructure.Evaluation;

/// <summary>
/// SQL-backed read of the raw FR-016 evaluation signals. Counts are deterministic reads of persisted data;
/// the derived aggregation (grouped duplicates, latest-feedback tally, items-per-brief) is applied by
/// <see cref="EvaluationService"/>.
/// </summary>
public sealed class SqlEvaluationRepository(PersonalTechBriefDbContext dbContext) : IEvaluationRepository
{
    public async Task<EvaluationSignals> GetSignalsAsync(CancellationToken cancellationToken)
    {
        var itemsIngested = await dbContext.SourceItems.AsNoTracking().CountAsync(cancellationToken);

        var itemsSelected = await (
            from item in dbContext.BriefItems.AsNoTracking()
            join brief in dbContext.Briefs.AsNoTracking() on item.BriefId equals brief.Id
            where brief.Status == BriefStatus.Completed
            select item.Id).CountAsync(cancellationToken);

        var totalSupportingSourceLinks = await dbContext.TechnologyUpdateSources.AsNoTracking().CountAsync(cancellationToken);
        var updatesWithSupportingSources = await dbContext.TechnologyUpdateSources.AsNoTracking()
            .Select(association => association.TechnologyUpdateId)
            .Distinct()
            .CountAsync(cancellationToken);

        var sourceOpens = await dbContext.SourceOpenEvents.AsNoTracking().CountAsync(cancellationToken);
        var saves = await dbContext.SavedUpdates.AsNoTracking().CountAsync(cancellationToken);
        var completedBriefCount = await dbContext.Briefs.AsNoTracking()
            .CountAsync(brief => brief.Status == BriefStatus.Completed, cancellationToken);

        var feedbackRows = await dbContext.UserFeedback.AsNoTracking()
            .Select(feedback => new
            {
                feedback.TechnologyUpdateId,
                feedback.Id,
                feedback.FeedbackType,
                feedback.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        var feedback = feedbackRows
            .Select(row => new FeedbackRecord(
                row.TechnologyUpdateId,
                row.Id,
                row.FeedbackType,
                DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc)))
            .ToList();

        var sourceDistribution = await LoadSourceDistributionAsync(cancellationToken);

        return new EvaluationSignals(
            itemsIngested,
            itemsSelected,
            totalSupportingSourceLinks,
            updatesWithSupportingSources,
            sourceOpens,
            saves,
            completedBriefCount,
            feedback,
            sourceDistribution);
    }

    private async Task<IReadOnlyList<SourceDistributionItem>> LoadSourceDistributionAsync(CancellationToken cancellationToken)
    {
        // Ingested items grouped by their configured source (manual submissions with no source are omitted).
        var ingestedBySource = await dbContext.SourceItems.AsNoTracking()
            .Where(item => item.SourceId != null)
            .GroupBy(item => item.SourceId!.Value)
            .Select(group => new { SourceId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        // The distinct source items that supported a selected update in a completed brief.
        var selectedSourceItemIds = await (
            from source in dbContext.BriefItemSources.AsNoTracking()
            join item in dbContext.BriefItems.AsNoTracking() on source.BriefItemId equals item.Id
            join brief in dbContext.Briefs.AsNoTracking() on item.BriefId equals brief.Id
            where brief.Status == BriefStatus.Completed
            select source.SourceItemId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var selectedBySource = await dbContext.SourceItems.AsNoTracking()
            .Where(item => item.SourceId != null && selectedSourceItemIds.Contains(item.Id))
            .GroupBy(item => item.SourceId!.Value)
            .Select(group => new { SourceId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var sourceNames = await dbContext.Sources.AsNoTracking()
            .Select(source => new { source.Id, source.Name })
            .ToListAsync(cancellationToken);

        var names = sourceNames.ToDictionary(source => source.Id, source => source.Name);
        var selected = selectedBySource.ToDictionary(row => row.SourceId, row => row.Count);

        return ingestedBySource
            .Select(row => new SourceDistributionItem(
                row.SourceId,
                names.TryGetValue(row.SourceId, out var name) ? name : string.Empty,
                row.Count,
                selected.TryGetValue(row.SourceId, out var selectedCount) ? selectedCount : 0))
            .OrderBy(item => item.SourceName, StringComparer.Ordinal)
            .ThenBy(item => item.SourceId)
            .ToList();
    }
}
