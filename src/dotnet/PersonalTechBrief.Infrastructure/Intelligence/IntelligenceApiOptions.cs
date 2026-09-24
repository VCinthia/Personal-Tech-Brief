namespace PersonalTechBrief.Infrastructure.Intelligence;

/// <summary>
/// Configuration for the typed Intelligence API client. Bound from the
/// <see cref="SectionName"/> configuration section; the base address is never hardcoded.
/// </summary>
public sealed class IntelligenceApiOptions
{
    public const string SectionName = "IntelligenceApi";

    /// <summary>Absolute base address of the internal Intelligence API (e.g. its internal cluster URL).</summary>
    public Uri? BaseAddress { get; init; }

    /// <summary>Per-request timeout for the analyze endpoint. Contract budget is 30s (spec §20).</summary>
    public int AnalyzeTimeoutSeconds { get; init; } = 30;

    /// <summary>Per-request timeout for the similarity endpoint. Similarity performs no model call.</summary>
    public int SimilarityTimeoutSeconds { get; init; } = 15;

    /// <summary>Per-attempt timeout for the generate endpoint. Contract budget is 60s (spec §20).</summary>
    public int GenerateTimeoutSeconds { get; init; } = 60;

    /// <summary>Maximum retries for transient transport failures (no response received).</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Base backoff delay in milliseconds; exponential with jitter is applied on top.</summary>
    public int RetryBaseDelayMilliseconds { get; init; } = 200;
}
