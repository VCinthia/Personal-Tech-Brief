using PersonalTechBrief.Domain.Ingestion;

namespace PersonalTechBrief.Application.Analysis;

/// <summary>Projection of a source item used to drive the grouping/relevance pipeline.</summary>
public sealed record GroupingSourceItem(
    Guid Id,
    Guid? SourceId,
    string Title,
    string? Excerpt,
    DateTime? PublishedAtUtc,
    DateTime RetrievedAtUtc,
    SourceItemProcessingStatus ProcessingStatus,
    int FailureCount,
    string? SupportingHost,
    Guid? ExistingTechnologyUpdateId)
{
    /// <summary>The observation time used for recency: the published time when known, else retrieval.</summary>
    public DateTime ObservedAtUtc => PublishedAtUtc ?? RetrievedAtUtc;

    public bool IsInTerminalState =>
        ProcessingStatus is SourceItemProcessingStatus.Processed or
            SourceItemProcessingStatus.Filtered or SourceItemProcessingStatus.FailedTerminal;

    public bool IsAlreadyGrouped => ExistingTechnologyUpdateId is not null;
}

/// <summary>Bounded candidate-finding query (recent window + topic overlap + max comparisons).</summary>
public sealed record GroupingCandidateQuery(
    DateTime ObservedWindowStartUtc,
    IReadOnlyList<string> Topics,
    int MaxComparisons);

/// <summary>A bounded grouping candidate representative for the similarity comparison.</summary>
public sealed record GroupingCandidate(Guid TechnologyUpdateId, string RepresentativeText);

/// <summary>One matched interest to persist against the update.</summary>
public sealed record InterestMatchRecord(Guid InterestId, double MatchStrength);

/// <summary>Group-level signals computed inside the commit transaction, fed to the pure scorer.</summary>
public sealed record GroupScoringSignals(
    DateTime NewestSupportingTimestampUtc,
    int DistinctSupportingHostCount,
    IReadOnlyList<InterestMatchRecord> GroupInterestMatches);

/// <summary>
/// Command to link or create a <see cref="Domain.Analysis.TechnologyUpdate"/> for a source item and
/// store its relevance. When <see cref="MergeTargetTechnologyUpdateId"/> is null a new update is created.
/// </summary>
public sealed record CommitGroupingCommand(
    Guid SourceItemId,
    Guid? MergeTargetTechnologyUpdateId,
    double? SimilarityScore,
    string RepresentativeTitle,
    string PrimaryTopic,
    DateTime ObservedAtUtc,
    IReadOnlyList<InterestMatchRecord> InterestMatches,
    DateTime UtcNow);

/// <summary>Outcome of a committed grouping decision.</summary>
public sealed record GroupingCommitResult(Guid TechnologyUpdateId, bool Created, double RelevanceScore);
