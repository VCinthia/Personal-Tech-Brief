namespace PersonalTechBrief.Infrastructure.Articles;

/// <summary>
/// Bounded outbound-fetch policy for one-off manual article URLs (FR-003). These are
/// operational limits, never product behavior.
/// </summary>
public sealed class ManualArticleFetchOptions
{
    public const string SectionName = "ManualArticleFetch";

    public int TimeoutSeconds { get; init; } = 10;

    public long MaximumResponseBytes { get; init; } = 5_242_880;

    public int MaxRedirects { get; init; } = 5;

    // A browser-like User-Agent so article hosts that reject unknown/bot agents can still be
    // fetched for one-off extraction. Override via configuration if a deployment needs otherwise.
    public string UserAgent { get; init; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36";
}
