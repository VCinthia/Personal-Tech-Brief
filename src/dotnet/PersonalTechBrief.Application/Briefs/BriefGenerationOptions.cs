namespace PersonalTechBrief.Application.Briefs;

/// <summary>
/// Configurable brief generation parameters (calibration §20). Defaults reproduce the frozen model:
/// look back 7 days when no previous brief exists, at most 5 visible items, and up to 3 bounded
/// transient retries per candidate generation. The selection threshold is not duplicated here; it is
/// reused from the Slice 5 relevance configuration.
/// </summary>
public sealed class BriefGenerationOptions
{
    public const string SectionName = "BriefGeneration";

    /// <summary>Look-back for the first brief when no previous completed brief exists.</summary>
    public int WindowLookbackDays { get; init; } = 7;

    /// <summary>Hard maximum of visible items in a brief (§20: 5, no minimum).</summary>
    public int MaxItems { get; init; } = 5;

    /// <summary>Bounded transient-retry attempts per candidate generation call (§20: up to 3).</summary>
    public int MaxGenerationAttempts { get; init; } = 3;

    /// <summary>Base backoff delay in milliseconds between generation retries; exponential with jitter on top.</summary>
    public int RetryBaseDelayMilliseconds { get; init; } = 200;

    /// <summary>Identifies the deterministic brief-generation pipeline version, persisted on the brief.</summary>
    public string GenerationVersion { get; init; } = "brief-1";
}
