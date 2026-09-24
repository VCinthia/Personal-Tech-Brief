namespace PersonalTechBrief.Application.Analysis;

/// <summary>
/// Configurable weights and thresholds for the deterministic relevance score (calibration §20).
/// Every constant lives here rather than being scattered across layers; defaults reproduce the
/// frozen calibration model exactly.
/// </summary>
public sealed class RelevanceScoringOptions
{
    public const string SectionName = "Relevance:Scoring";

    /// <summary>Base points for the strongest matched active interest, before the strength multiplier.</summary>
    public double InterestHighWeight { get; init; } = 60d;

    public double InterestMediumWeight { get; init; } = 40d;

    public double InterestLowWeight { get; init; } = 20d;

    /// <summary>Points contributed per additional matched interest, scaled by its clamped strength.</summary>
    public double AdditionalInterestPointsPerMatch { get; init; } = 5d;

    /// <summary>Upper bound for the combined additional-interest bonus.</summary>
    public double AdditionalInterestBonusCap { get; init; } = 10d;

    /// <summary>Recency bucket: newest supporting source at or within this many hours.</summary>
    public int RecencyRecentHours { get; init; } = 24;

    public double RecencyRecentPoints { get; init; } = 20d;

    public int RecencyRecentDaysHours { get; init; } = 72;

    public double RecencyRecentDaysPoints { get; init; } = 12d;

    public int RecencyWeekHours { get; init; } = 168;

    public double RecencyWeekPoints { get; init; } = 5d;

    public double ImpactHighPoints { get; init; } = 20d;

    public double ImpactMediumPoints { get; init; } = 10d;

    /// <summary>Independent-source support: distinct supporting hosts of exactly two.</summary>
    public double SourceSupportTwoHostsPoints { get; init; } = 4d;

    /// <summary>Independent-source support: three or more distinct supporting hosts.</summary>
    public double SourceSupportThreeOrMoreHostsPoints { get; init; } = 7d;

    /// <summary>Selection threshold (not persisted per item in this slice; used at brief time).</summary>
    public double SelectionThreshold { get; init; } = 55d;
}
