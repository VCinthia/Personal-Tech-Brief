using PersonalTechBrief.Domain.Interactions;

namespace PersonalTechBrief.Application.Evaluation;

/// <summary>One latest-per-update feedback row as read for evaluation (the current feedback, AC-012).</summary>
public sealed record FeedbackRecord(Guid TechnologyUpdateId, Guid FeedbackId, FeedbackType FeedbackType, DateTime CreatedAtUtc);

/// <summary>Selected/ingested item counts for one source (FR-016 source distribution).</summary>
public sealed record SourceDistributionItem(Guid SourceId, string SourceName, int IngestedItems, int SelectedItems);

/// <summary>
/// Raw evaluation inputs read from persistence. The derived FR-016 signals (grouped duplicates, the
/// latest-feedback tally, and items-per-brief) are computed by <see cref="EvaluationService"/> from these,
/// which keeps that logic unit-testable with a fake repository.
/// </summary>
public sealed record EvaluationSignals(
    int ItemsIngested,
    int ItemsSelected,
    int TotalSupportingSourceLinks,
    int UpdatesWithSupportingSources,
    int SourceOpens,
    int Saves,
    int CompletedBriefCount,
    IReadOnlyList<FeedbackRecord> Feedback,
    IReadOnlyList<SourceDistributionItem> SourceDistribution);

/// <summary>The read-only FR-016 product-evaluation summary returned by <c>GET /api/v1/evaluation/summary</c>.</summary>
public sealed record EvaluationSummary(
    int ItemsIngested,
    int ItemsSelected,
    int GroupedDuplicates,
    int RelevantFeedback,
    int NotRelevantFeedback,
    int SourceOpens,
    int Saves,
    int BriefCount,
    double ItemsPerBrief,
    IReadOnlyList<SourceDistributionItem> SourceDistribution);
