namespace PersonalTechBrief.Application.Messaging;

/// <summary>
/// Message metadata received from the internal content-processing queue. The body is
/// deliberately kept opaque until the processor validates the version-one envelope.
/// </summary>
public sealed record ContentProcessingReceivedMessage(
    string MessageId,
    string Body,
    string? CorrelationId,
    string? TraceParent,
    int DeliveryCount);

public enum ContentProcessingMessageDispositionKind
{
    Complete,
    Abandon,
    DeadLetter,
}

/// <summary>
/// The worker chooses a bounded broker settlement. Retries remain broker-owned; this
/// contract deliberately has no application-level replay loop.
/// </summary>
public sealed record ContentProcessingMessageDisposition(
    ContentProcessingMessageDispositionKind Kind,
    string? DeadLetterReason = null,
    string? DeadLetterDescription = null)
{
    public const int DeadLetterReasonMaxLength = 128;
    public const int DeadLetterDescriptionMaxLength = 1_024;

    public static ContentProcessingMessageDisposition Complete() =>
        new(ContentProcessingMessageDispositionKind.Complete);

    public static ContentProcessingMessageDisposition Abandon() =>
        new(ContentProcessingMessageDispositionKind.Abandon);

    public static ContentProcessingMessageDisposition DeadLetter(string reason, string description)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > DeadLetterReasonMaxLength)
        {
            throw new ArgumentException("A bounded dead-letter reason is required.", nameof(reason));
        }

        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > DeadLetterDescriptionMaxLength)
        {
            throw new ArgumentException("A bounded dead-letter description is required.", nameof(description));
        }

        return new(
            ContentProcessingMessageDispositionKind.DeadLetter,
            reason.Trim(),
            description.Trim());
    }
}

/// <summary>
/// Adapter boundary for one bounded receive/settle cycle. The adapter owns native
/// Service Bus message handles so application code stays free of broker SDK types.
/// </summary>
public interface IContentProcessingMessageReceiver
{
    Task ReceiveAndDispatchOnceAsync(
        Func<ContentProcessingReceivedMessage, CancellationToken, Task<ContentProcessingMessageDisposition>> handler,
        CancellationToken cancellationToken);
}

/// <summary>
/// Adapter boundary used by the transactional-outbox dispatcher.
/// </summary>
public interface IContentProcessingPublisher
{
    Task PublishAsync(SourceItemReadyEnvelope envelope, CancellationToken cancellationToken);
}
