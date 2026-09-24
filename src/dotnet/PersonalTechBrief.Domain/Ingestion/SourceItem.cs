using System.Security.Cryptography;
using System.Text;

namespace PersonalTechBrief.Domain.Ingestion;

public sealed class SourceItem
{
    private SourceItem()
    {
    }

    private SourceItem(
        Guid sourceId,
        string? externalId,
        string? sourceUrl,
        string title,
        string? excerpt,
        DateTime? publishedAtUtc,
        string? contentHash,
        DateTime retrievedAtUtc)
    {
        if (sourceId == Guid.Empty)
        {
            throw new ArgumentException("A feed item requires a source identifier.", nameof(sourceId));
        }

        EnsureUtc(retrievedAtUtc, nameof(retrievedAtUtc));
        if (publishedAtUtc.HasValue)
        {
            EnsureUtc(publishedAtUtc.Value, nameof(publishedAtUtc));
        }

        var (canonicalUrl, normalizedUrl) = SourceItemUrl.Prepare(sourceUrl);
        Id = Guid.NewGuid();
        SourceId = sourceId;
        OriginType = SourceItemOriginType.Feed;
        ExternalId = SourceItemText.CleanExternalId(externalId);
        OriginalUrl = string.IsNullOrWhiteSpace(sourceUrl) ? null : sourceUrl.Trim();
        CanonicalUrl = canonicalUrl;
        NormalizedUrl = normalizedUrl;
        NormalizedUrlHash = normalizedUrl is null
            ? null
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedUrl)));
        Title = SourceItemText.CleanTitle(title);
        NormalizedTitle = SourceItemText.NormalizeTitle(title);
        Excerpt = SourceItemText.CleanExcerpt(excerpt);
        PublishedAtUtc = publishedAtUtc;
        RetrievedAtUtc = retrievedAtUtc;
        ContentHash = SourceItemText.CleanContentHash(contentHash);
        ProcessingStatus = SourceItemProcessingStatus.Queued;
        CreatedAtUtc = retrievedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid? SourceId { get; private set; }

    public SourceItemOriginType OriginType { get; private set; }

    public string? ExternalId { get; private set; }

    public string? OriginalUrl { get; private set; }

    public string? CanonicalUrl { get; private set; }

    public string? NormalizedUrl { get; private set; }

    public string? NormalizedUrlHash { get; private set; }

    public string Title { get; private set; } = null!;

    public string NormalizedTitle { get; private set; } = null!;

    public string? Excerpt { get; private set; }

    public DateTime? PublishedAtUtc { get; private set; }

    public DateTime RetrievedAtUtc { get; private set; }

    public string? ContentHash { get; private set; }

    public SourceItemProcessingStatus ProcessingStatus { get; private set; }

    public int FailureCount { get; private set; }

    public string? LastFailureCode { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static SourceItem CreateFeed(
        Guid sourceId,
        string? externalId,
        string? sourceUrl,
        string title,
        string? excerpt,
        DateTime? publishedAtUtc,
        string? contentHash,
        DateTime retrievedAtUtc) =>
        new(sourceId, externalId, sourceUrl, title, excerpt, publishedAtUtc, contentHash, retrievedAtUtc);

    /// <summary>
    /// Creates a source-less item from a one-off manually submitted article URL (FR-003).
    /// It has no owning <see cref="SourceId"/> and no external feed identifier, so submitting a
    /// URL never registers its host as a permanent subscribed source. The submitted URL is
    /// required and its title/excerpt come from bounded server-side extraction, never raw HTML.
    /// </summary>
    public static SourceItem CreateManual(
        string articleUrl,
        string title,
        string? excerpt,
        string? contentHash,
        DateTime retrievedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(articleUrl))
        {
            throw new ArgumentException("A manual article requires a submitted URL.", nameof(articleUrl));
        }

        EnsureUtc(retrievedAtUtc, nameof(retrievedAtUtc));

        var (canonicalUrl, normalizedUrl) = SourceItemUrl.Prepare(articleUrl);
        var item = new SourceItem
        {
            Id = Guid.NewGuid(),
            SourceId = null,
            OriginType = SourceItemOriginType.ManualUrl,
            ExternalId = null,
            OriginalUrl = articleUrl.Trim(),
            CanonicalUrl = canonicalUrl,
            NormalizedUrl = normalizedUrl,
            NormalizedUrlHash = normalizedUrl is null
                ? null
                : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedUrl))),
            Title = SourceItemText.CleanTitle(title),
            NormalizedTitle = SourceItemText.NormalizeTitle(title),
            Excerpt = SourceItemText.CleanExcerpt(excerpt),
            PublishedAtUtc = null,
            RetrievedAtUtc = retrievedAtUtc,
            ContentHash = SourceItemText.CleanContentHash(contentHash),
            ProcessingStatus = SourceItemProcessingStatus.Queued,
            CreatedAtUtc = retrievedAtUtc,
        };
        return item;
    }

    /// <summary>
    /// Advances the item to the terminal <see cref="SourceItemProcessingStatus.Processed"/> state
    /// after grouping/relevance completes. Idempotent when already processed so at-least-once
    /// redelivery is a no-op; rejected from a competing terminal state.
    /// </summary>
    public void MarkProcessed()
    {
        if (ProcessingStatus == SourceItemProcessingStatus.Processed)
        {
            return;
        }

        if (ProcessingStatus is SourceItemProcessingStatus.Filtered or SourceItemProcessingStatus.FailedTerminal)
        {
            throw new InvalidOperationException(
                $"A source item in the terminal state {ProcessingStatus} cannot be marked processed.");
        }

        ProcessingStatus = SourceItemProcessingStatus.Processed;
    }

    /// <summary>
    /// Records a processing failure for the bounded-retry policy. A retryable failure increments the
    /// failure count and leaves the item eligible for another attempt; a terminal failure stops the
    /// bounded retry loop. Ignored once the item has already reached a terminal state.
    /// </summary>
    public void RecordProcessingFailure(bool terminal, string? failureCode)
    {
        if (ProcessingStatus is SourceItemProcessingStatus.Processed or
            SourceItemProcessingStatus.Filtered or SourceItemProcessingStatus.FailedTerminal)
        {
            return;
        }

        FailureCount++;
        LastFailureCode = string.IsNullOrWhiteSpace(failureCode)
            ? null
            : failureCode.Trim()[..Math.Min(failureCode.Trim().Length, 128)];
        ProcessingStatus = terminal
            ? SourceItemProcessingStatus.FailedTerminal
            : SourceItemProcessingStatus.FailedRetryable;
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Source item timestamps must be UTC.", parameterName);
        }
    }
}
