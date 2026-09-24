namespace PersonalTechBrief.Infrastructure.Feeds;

public sealed class FeedValidationOptions
{
    public const string SectionName = "FeedValidation";

    public int TimeoutSeconds { get; init; } = 15;

    public int MaximumResponseBytes { get; init; } = 1_048_576;

    // A browser-like User-Agent so feeds that reject unknown/bot agents (e.g. github.blog) can be
    // validated. This is a personal single-user reader fetching public feeds the user chose;
    // override via configuration if a specific deployment needs a different agent.
    public string UserAgent { get; init; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36";
}
