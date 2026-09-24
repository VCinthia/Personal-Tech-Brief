namespace PersonalTechBrief.Application.Articles;

/// <summary>
/// Outcome of a one-off manual article URL submission (FR-003, UC-004, AC-011).
/// </summary>
public enum ManualArticleSubmissionOutcome
{
    /// <summary>A new source-less item was persisted and enqueued for processing.</summary>
    Queued,

    /// <summary>The URL was already known; no new item or outbox message was created.</summary>
    Duplicate,

    /// <summary>The URL is missing, malformed, not absolute http/https, or a reserved host.</summary>
    InvalidUrl,

    /// <summary>The URL is well-formed but could not be safely fetched (blocked host, redirect
    /// leaving the safe set, timeout, size cap, or a non-HTML response).</summary>
    Unfetchable,

    /// <summary>The page was fetched but no usable title/text could be extracted.</summary>
    NotExtractable,
}

public sealed record ManualArticleSubmissionResult(
    ManualArticleSubmissionOutcome Outcome,
    Guid? SourceItemId)
{
    public static ManualArticleSubmissionResult Queued(Guid sourceItemId) =>
        new(ManualArticleSubmissionOutcome.Queued, sourceItemId);

    public static ManualArticleSubmissionResult Duplicate(Guid? sourceItemId) =>
        new(ManualArticleSubmissionOutcome.Duplicate, sourceItemId);

    public static ManualArticleSubmissionResult InvalidUrl() =>
        new(ManualArticleSubmissionOutcome.InvalidUrl, null);

    public static ManualArticleSubmissionResult Unfetchable() =>
        new(ManualArticleSubmissionOutcome.Unfetchable, null);

    public static ManualArticleSubmissionResult NotExtractable() =>
        new(ManualArticleSubmissionOutcome.NotExtractable, null);
}

/// <summary>
/// Accepts a single user-submitted article URL, fetches and extracts it safely, and hands the
/// result to the normal processing pipeline without registering the host as a permanent source.
/// </summary>
public interface IManualArticleSubmissionService
{
    Task<ManualArticleSubmissionResult> SubmitAsync(string? url, CancellationToken cancellationToken);
}

public sealed record PersistManualArticleCommand(
    string ArticleUrl,
    string Title,
    string? Excerpt,
    string? ContentHash,
    Guid CorrelationId,
    DateTime RetrievedAtUtc,
    string? TraceParent);

public sealed record PersistManualArticleResult(
    bool IsDuplicate,
    Guid? SourceItemId,
    Guid? OutboxMessageId)
{
    public static PersistManualArticleResult Duplicate(Guid? sourceItemId) => new(true, sourceItemId, null);

    public static PersistManualArticleResult Persisted(Guid sourceItemId, Guid outboxMessageId) =>
        new(false, sourceItemId, outboxMessageId);
}

/// <summary>
/// Persistence port for a source-less manual article. Applies the deterministic duplicate
/// precedence and, for a new item, atomically stores the <c>ManualUrl</c> source item and its
/// pending outbox message in one transaction. It never touches Sources or IngestionRuns.
/// </summary>
public interface IManualArticleStore
{
    Task<PersistManualArticleResult> PersistIfNewAsync(
        PersistManualArticleCommand command,
        CancellationToken cancellationToken);
}
