using PersonalTechBrief.Application.Briefs;
using PersonalTechBrief.Domain.Briefs;

namespace PersonalTechBrief.Web.Briefs;

/// <summary>Response for <c>POST /api/v1/briefs</c> (202 Accepted).</summary>
public sealed record CreateBriefResponse(Guid Id, BriefStatus Status)
{
    public static CreateBriefResponse From(Brief brief) => new(brief.Id, brief.Status);
}

/// <summary>Header projection of a brief for history listings.</summary>
public sealed record BriefSummaryResponse(
    Guid Id,
    DateTime GeneratedAtUtc,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    BriefStatus Status,
    int CandidateCount,
    int SelectedCount,
    string GenerationVersion,
    Guid CorrelationId)
{
    public static BriefSummaryResponse From(BriefSummaryReadModel summary) => new(
        summary.Id,
        summary.GeneratedAtUtc,
        summary.WindowStartUtc,
        summary.WindowEndUtc,
        summary.Status,
        summary.CandidateCount,
        summary.SelectedCount,
        summary.GenerationVersion,
        summary.CorrelationId);
}

/// <summary>A full brief with its immutable item snapshots.</summary>
public sealed record BriefResponse(BriefSummaryResponse Brief, IReadOnlyList<BriefItemResponse> Items)
{
    public static BriefResponse From(BriefReadModel model) => new(
        BriefSummaryResponse.From(model.Summary),
        model.Items.Select(BriefItemResponse.From).ToList());
}

/// <summary>One immutable brief item snapshot with its source references.</summary>
public sealed record BriefItemResponse(
    Guid Id,
    Guid TechnologyUpdateId,
    int Rank,
    string Title,
    string Topic,
    string Summary,
    string WhyRelevant,
    double RelevanceScore,
    DateTime GeneratedAtUtc,
    string PromptVersion,
    string ModelOrAlgorithmVersion,
    IReadOnlyList<BriefItemSourceResponse> Sources,
    string? CurrentFeedback,
    bool IsSaved)
{
    public static BriefItemResponse From(BriefItemReadModel item) => new(
        item.Id,
        item.TechnologyUpdateId,
        item.Rank,
        item.Title,
        item.Topic,
        item.Summary,
        item.WhyRelevant,
        item.RelevanceScore,
        item.GeneratedAtUtc,
        item.PromptVersion,
        item.ModelOrAlgorithmVersion,
        item.Sources.Select(BriefItemSourceResponse.From).ToList(),
        item.CurrentFeedback,
        item.IsSaved);
}

/// <summary>A supporting source reference with a way to open the original source (FR-012/AC-010).</summary>
public sealed record BriefItemSourceResponse(Guid SourceItemId, string Title, string? Url, DateTime? PublishedAtUtc)
{
    public static BriefItemSourceResponse From(BriefItemSourceReadModel source) =>
        new(source.SourceItemId, source.Title, source.Url, source.PublishedAtUtc);
}

/// <summary>A page of brief history.</summary>
public sealed record BriefHistoryResponse(IReadOnlyList<BriefSummaryResponse> Briefs, string? NextCursor)
{
    public static BriefHistoryResponse From(BriefPage page) => new(
        page.Briefs.Select(BriefSummaryResponse.From).ToList(),
        page.NextCursor);
}
