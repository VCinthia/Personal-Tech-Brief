namespace PersonalTechBrief.Application.Ingestion;

public sealed record PersistSourceItemCommand(
    Guid SourceId,
    Guid IngestionRunId,
    Guid CorrelationId,
    string? ExternalId,
    string? SourceUrl,
    string Title,
    string? Excerpt,
    DateTime? PublishedAtUtc,
    string? ContentHash,
    DateTime RetrievedAtUtc,
    string? TraceParent);

public sealed record PersistSourceItemResult(
    bool IsDuplicate,
    Guid? SourceItemId,
    Guid? OutboxMessageId)
{
    public static PersistSourceItemResult Duplicate() => new(true, null, null);

    public static PersistSourceItemResult Persisted(Guid sourceItemId, Guid outboxMessageId) =>
        new(false, sourceItemId, outboxMessageId);
}

public interface ISourceItemPersistenceService
{
    /// <summary>
    /// Applies the approved deterministic duplicate precedence and, for a new feed item,
    /// atomically stores the source item and its pending outbox message.
    /// </summary>
    Task<PersistSourceItemResult> PersistIfNewAsync(
        PersistSourceItemCommand command,
        CancellationToken cancellationToken);
}
