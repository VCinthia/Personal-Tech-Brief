using PersonalTechBrief.Domain.Interests;

namespace PersonalTechBrief.Application.Briefs;

/// <summary>
/// A candidate technology update considered for a brief. Deterministic selection (§20) filters by
/// threshold, window and the already-briefed rule, then ranks by score descending with a stable
/// tie-break on <see cref="TechnologyUpdateId"/>.
/// </summary>
public sealed record BriefCandidate(
    Guid TechnologyUpdateId,
    double RelevanceScore,
    DateTime LastObservedAtUtc,
    bool AlreadyBriefed);

/// <summary>The window a brief covers: from the previous completed brief time (else now-7d) to now.</summary>
public sealed record BriefWindow(DateTime StartUtc, DateTime EndUtc);

/// <summary>Result of deterministic candidate selection.</summary>
public sealed record BriefSelection(int CandidateCount, IReadOnlyList<BriefCandidate> Selected);

/// <summary>The persisted material used to build one candidate's generation request (persisted data only).</summary>
public sealed record BriefGenerationInput(
    Guid TechnologyUpdateId,
    string PrimaryTopic,
    IReadOnlyList<BriefSourceInput> Sources,
    IReadOnlyList<BriefInterestInput> Interests);

/// <summary>A supporting source item for a candidate (title/excerpt/url/published).</summary>
public sealed record BriefSourceInput(
    Guid SourceItemId,
    string Title,
    string? Excerpt,
    string? SourceUrl,
    DateTime? PublishedAtUtc);

/// <summary>A matched interest for a candidate.</summary>
public sealed record BriefInterestInput(Guid InterestId, string Name, InterestPriority Priority);
