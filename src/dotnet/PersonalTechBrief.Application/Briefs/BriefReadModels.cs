using PersonalTechBrief.Domain.Briefs;

namespace PersonalTechBrief.Application.Briefs;

/// <summary>Read projection of a brief's header fields (data model §11).</summary>
public sealed record BriefSummaryReadModel(
    Guid Id,
    DateTime GeneratedAtUtc,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    BriefStatus Status,
    int CandidateCount,
    int SelectedCount,
    string GenerationVersion,
    Guid CorrelationId);

/// <summary>Read projection of a full brief with its immutable item snapshots.</summary>
public sealed record BriefReadModel(BriefSummaryReadModel Summary, IReadOnlyList<BriefItemReadModel> Items);

/// <summary>Read projection of one immutable brief item snapshot with its source references.</summary>
public sealed record BriefItemReadModel(
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
    IReadOnlyList<BriefItemSourceReadModel> Sources,
    // Live per-update interaction state surfaced alongside the immutable snapshot (Slice 7.1); the
    // snapshot fields above are never mutated. CurrentFeedback is the latest feedback wire value
    // ("relevant"/"notRelevant") or null.
    string? CurrentFeedback,
    bool IsSaved);

/// <summary>Read projection of one supporting source reference (FR-012/AC-010).</summary>
public sealed record BriefItemSourceReadModel(
    Guid SourceItemId,
    string Title,
    string? Url,
    DateTime? PublishedAtUtc);

/// <summary>A page of brief history (paginates briefs, not the ingestion corpus).</summary>
public sealed record BriefPage(IReadOnlyList<BriefSummaryReadModel> Briefs, string? NextCursor);
