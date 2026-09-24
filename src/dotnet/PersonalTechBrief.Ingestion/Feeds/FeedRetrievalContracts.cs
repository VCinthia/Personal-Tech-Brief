using PersonalTechBrief.Domain.Sources;

namespace PersonalTechBrief.Ingestion.Feeds;

/// <summary>
/// Bounded, one-shot retrieval boundary for configured RSS and Atom sources. Implementations do
/// not persist items or publish messages; those responsibilities remain with the ingestion job and
/// the transactional persistence boundary respectively.
/// </summary>
public interface IFeedRetrievalClient
{
    Task<FeedRetrievalResult> RetrieveAsync(Source source, CancellationToken cancellationToken);
}

public sealed record FeedRetrievalResult(
    bool IsNotModified,
    IReadOnlyList<RetrievedFeedItem> Items,
    string? ETag,
    DateTimeOffset? LastModified)
{
    public static FeedRetrievalResult NotModified(string? eTag, DateTimeOffset? lastModified) =>
        new(true, [], eTag, lastModified);
}

/// <summary>
/// Plain, source-derived feed values. Values are normalized and validated by the frozen
/// <c>ISourceItemPersistenceService</c> before they reach SQL or the transactional outbox.
/// </summary>
public sealed record RetrievedFeedItem(
    string? ExternalId,
    string? SourceUrl,
    string Title,
    string? Excerpt,
    DateTime? PublishedAtUtc,
    string? ContentHash);

/// <summary>
/// A bounded, safe diagnostic classification for one source retrieval. It intentionally contains
/// no raw response/feed text or source URL so callers can record and log it safely.
/// </summary>
public sealed class FeedRetrievalException(
    string errorCode,
    string safeDetail,
    bool isTransient = false,
    Exception? innerException = null) : Exception(safeDetail, innerException)
{
    public string ErrorCode { get; } = errorCode;

    public string SafeDetail { get; } = safeDetail;

    public bool IsTransient { get; } = isTransient;
}
