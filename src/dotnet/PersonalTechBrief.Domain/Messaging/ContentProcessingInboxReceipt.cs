namespace PersonalTechBrief.Domain.Messaging;

public enum ContentProcessingInboxStatus
{
    Pending,
    Processed,
}

public sealed class ContentProcessingInboxReceipt
{
    private ContentProcessingInboxReceipt()
    {
    }

    public Guid SourceItemId { get; private set; }

    public Guid MessageId { get; private set; }

    public Guid CorrelationId { get; private set; }

    public string EnvelopeJson { get; private set; } = null!;

    public DateTime ReceivedAtUtc { get; private set; }

    public ContentProcessingInboxStatus Status { get; private set; }

    /// <summary>
    /// Marks the receipt terminally processed once the grouping/relevance pipeline has handled the
    /// source item. Idempotent, so a redelivered or replayed completion is a no-op.
    /// </summary>
    public void MarkProcessed() => Status = ContentProcessingInboxStatus.Processed;

    public static ContentProcessingInboxReceipt CreatePending(
        Guid sourceItemId,
        Guid messageId,
        Guid correlationId,
        string envelopeJson,
        DateTime receivedAtUtc)
    {
        if (sourceItemId == Guid.Empty || messageId == Guid.Empty || correlationId == Guid.Empty)
        {
            throw new ArgumentException("An inbox receipt requires non-empty identifiers.");
        }

        if (string.IsNullOrWhiteSpace(envelopeJson))
        {
            throw new ArgumentException("An inbox receipt requires the validated envelope.", nameof(envelopeJson));
        }

        if (receivedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Inbox receipt time must be UTC.", nameof(receivedAtUtc));
        }

        return new ContentProcessingInboxReceipt
        {
            SourceItemId = sourceItemId,
            MessageId = messageId,
            CorrelationId = correlationId,
            EnvelopeJson = envelopeJson,
            ReceivedAtUtc = receivedAtUtc,
            Status = ContentProcessingInboxStatus.Pending,
        };
    }
}
