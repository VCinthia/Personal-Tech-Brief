namespace PersonalTechBrief.Ingestion.Feeds;

public sealed class FeedRetrievalOptions
{
    public const string SectionName = "FeedRetrieval";

    public int TimeoutSeconds { get; init; } = 15;

    public int MaximumResponseBytes { get; init; } = 1_048_576;

    public int MaximumAttempts { get; init; } = 3;

    public int RetryDelayMilliseconds { get; init; } = 250;
}
