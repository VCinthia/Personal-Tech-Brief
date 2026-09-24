using PersonalTechBrief.Domain.Interactions;

namespace PersonalTechBrief.Application.Evaluation;

/// <summary>
/// Assembles the FR-016 evaluation summary from raw <see cref="EvaluationSignals"/>. This is deterministic
/// product-effectiveness telemetry (no engagement scoring, no per-user profiling).
/// </summary>
public sealed class EvaluationService(IEvaluationRepository repository) : IEvaluationService
{
    public async Task<EvaluationSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var signals = await repository.GetSignalsAsync(cancellationToken);

        // Grouped duplicates: supporting-source links beyond the first per update (folded-in duplicates).
        var groupedDuplicates = Math.Max(0, signals.TotalSupportingSourceLinks - signals.UpdatesWithSupportingSources);

        // Latest feedback supersedes for evaluation: reduce to one current row per update, then tally.
        var latestPerUpdate = signals.Feedback
            .GroupBy(record => record.TechnologyUpdateId)
            .Select(group => group
                .OrderByDescending(record => record.CreatedAtUtc)
                .ThenByDescending(record => record.FeedbackId)
                .First());

        var relevantFeedback = 0;
        var notRelevantFeedback = 0;
        foreach (var current in latestPerUpdate)
        {
            if (current.FeedbackType == FeedbackType.Relevant)
            {
                relevantFeedback++;
            }
            else
            {
                notRelevantFeedback++;
            }
        }

        var itemsPerBrief = signals.CompletedBriefCount == 0
            ? 0d
            : (double)signals.ItemsSelected / signals.CompletedBriefCount;

        return new EvaluationSummary(
            signals.ItemsIngested,
            signals.ItemsSelected,
            groupedDuplicates,
            relevantFeedback,
            notRelevantFeedback,
            signals.SourceOpens,
            signals.Saves,
            signals.CompletedBriefCount,
            itemsPerBrief,
            signals.SourceDistribution);
    }
}
