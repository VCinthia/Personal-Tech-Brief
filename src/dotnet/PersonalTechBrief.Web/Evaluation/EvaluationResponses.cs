using PersonalTechBrief.Application.Evaluation;

namespace PersonalTechBrief.Web.Evaluation;

/// <summary>Response for <c>GET /api/v1/evaluation/summary</c> (FR-016 product-evaluation signals).</summary>
public sealed record EvaluationSummaryResponse(
    int ItemsIngested,
    int ItemsSelected,
    int GroupedDuplicates,
    int RelevantFeedback,
    int NotRelevantFeedback,
    int SourceOpens,
    int Saves,
    int BriefCount,
    double ItemsPerBrief,
    IReadOnlyList<SourceDistributionResponse> SourceDistribution)
{
    public static EvaluationSummaryResponse From(EvaluationSummary summary) => new(
        summary.ItemsIngested,
        summary.ItemsSelected,
        summary.GroupedDuplicates,
        summary.RelevantFeedback,
        summary.NotRelevantFeedback,
        summary.SourceOpens,
        summary.Saves,
        summary.BriefCount,
        summary.ItemsPerBrief,
        summary.SourceDistribution.Select(SourceDistributionResponse.From).ToList());
}

/// <summary>Per-source selected/ingested item counts.</summary>
public sealed record SourceDistributionResponse(Guid SourceId, string SourceName, int IngestedItems, int SelectedItems)
{
    public static SourceDistributionResponse From(SourceDistributionItem item) =>
        new(item.SourceId, item.SourceName, item.IngestedItems, item.SelectedItems);
}
