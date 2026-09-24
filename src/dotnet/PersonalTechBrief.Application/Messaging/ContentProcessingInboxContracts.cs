namespace PersonalTechBrief.Application.Messaging;

public enum ContentProcessingInboxAcceptance
{
    Accepted,
    AlreadyAccepted,
    SourceItemTerminal,
    SourceItemNotFound,
    ReferenceMismatch,
    InvalidSourceItemState,
}

public sealed record PendingContentProcessingReceipt(SourceItemReadyEnvelope Envelope, DateTime ReceivedAtUtc);

/// <summary>
/// Durable handoff owned by .NET. Successful acceptance commits a unique receipt before
/// broker completion; a fresh process can discover pending work without broker replay.
/// Later slices own semantic processing and receipt completion.
/// </summary>
public interface IContentProcessingInboxStore
{
    Task<ContentProcessingInboxAcceptance> AcceptAsync(
        SourceItemReadyEnvelope envelope,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PendingContentProcessingReceipt>> LoadPendingAsync(
        int batchSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks a receipt terminally processed after the grouping/relevance pipeline handled the item.
    /// Idempotent: a missing or already-processed receipt is a no-op, so at-least-once redelivery and
    /// replay never fail.
    /// </summary>
    Task MarkProcessedAsync(Guid sourceItemId, CancellationToken cancellationToken);
}
