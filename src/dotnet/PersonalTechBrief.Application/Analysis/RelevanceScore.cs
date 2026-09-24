using PersonalTechBrief.Domain.Interests;

namespace PersonalTechBrief.Application.Analysis;

/// <summary>Impact tier used by the relevance scorer, decoupled from the Intelligence API contract.</summary>
public enum ImpactTier
{
    High,
    Medium,
    Low,
    None,
}

/// <summary>One matched active interest feeding the interest component of the score.</summary>
public sealed record InterestMatchSignal(InterestPriority Priority, double MatchStrength);

/// <summary>Deterministic inputs to the relevance scorer (calibration §20). Pure data, no I/O.</summary>
public sealed record RelevanceScoreRequest(
    IReadOnlyList<InterestMatchSignal> InterestMatches,
    ImpactTier Impact,
    DateTime NewestSupportingTimestampUtc,
    DateTime EvaluatedAtUtc,
    int DistinctSupportingHostCount);

/// <summary>A single named contribution to the total score, with a short diagnostic explanation.</summary>
public sealed record RelevanceComponent(string Name, double Points, string Explanation);

/// <summary>The total relevance score plus an explainable per-component breakdown (BR-011 basis).</summary>
public sealed record RelevanceScore(
    double Total,
    bool MeetsSelectionThreshold,
    IReadOnlyList<RelevanceComponent> Components);
