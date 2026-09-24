using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PersonalTechBrief.Application.Messaging;

namespace PersonalTechBrief.Processor.Messaging;

/// <summary>
/// Slice 3's idempotent consumer boundary. It validates the durable envelope and
/// acknowledges an item only after committing a durable inbox receipt for a later
/// intelligence stage. It performs no Python call, filtering, or update creation.
/// </summary>
public sealed class SourceItemReadyMessageHandler(
    IContentProcessingInboxStore inboxStore,
    TimeProvider timeProvider,
    ILogger<SourceItemReadyMessageHandler> logger)
{
    private static readonly ActivitySource ActivitySource = new("PersonalTechBrief.Processor.Messaging");
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<ContentProcessingMessageDisposition> HandleAsync(
        ContentProcessingReceivedMessage message,
        CancellationToken cancellationToken)
    {
        if (!TryParseEnvelope(message, out var envelope, out var reason))
        {
            logger.LogWarning(
                "Dead-lettering malformed SourceItemReady message {MessageId}; reason {Reason}.",
                message.MessageId,
                reason);
            return ContentProcessingMessageDisposition.DeadLetter("SourceItemReadyMalformed", reason);
        }

        using var activity = StartMessageActivity(envelope.TraceParent);
        try
        {
            var acceptance = await inboxStore.AcceptAsync(
                envelope, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
            if (acceptance == ContentProcessingInboxAcceptance.SourceItemNotFound)
            {
                logger.LogWarning(
                    "Dead-lettering SourceItemReady message {MessageId} for missing source item {SourceItemId}; correlation {CorrelationId}.",
                    envelope.MessageId,
                    envelope.SourceItemId,
                    envelope.CorrelationId);
                return ContentProcessingMessageDisposition.DeadLetter(
                    "SourceItemNotFound",
                    "The referenced source item does not exist.");
            }

            if (acceptance is ContentProcessingInboxAcceptance.ReferenceMismatch or
                ContentProcessingInboxAcceptance.InvalidSourceItemState)
            {
                logger.LogWarning(
                    "Dead-lettering SourceItemReady message {MessageId}; validation {Acceptance}; correlation {CorrelationId}.",
                    envelope.MessageId, acceptance, envelope.CorrelationId);
                return ContentProcessingMessageDisposition.DeadLetter(
                    "SourceItemReferenceInvalid", "The message references do not match an eligible persisted source item.");
            }

            logger.LogInformation(
                "Completing SourceItemReady handoff message {MessageId} for source item {SourceItemId}; inbox outcome {Acceptance}; correlation {CorrelationId}.",
                envelope.MessageId,
                envelope.SourceItemId,
                acceptance,
                envelope.CorrelationId);
            return ContentProcessingMessageDisposition.Complete();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Deferring SourceItemReady message {MessageId} for source item {SourceItemId}; correlation {CorrelationId} to broker retry.",
                envelope.MessageId,
                envelope.SourceItemId,
                envelope.CorrelationId);
            return ContentProcessingMessageDisposition.Abandon();
        }
    }

    private static bool TryParseEnvelope(
        ContentProcessingReceivedMessage message,
        out SourceItemReadyEnvelope envelope,
        out string reason)
    {
        envelope = null!;
        reason = string.Empty;
        try
        {
            var parsed = JsonSerializer.Deserialize<SourceItemReadyEnvelope>(message.Body, SerializerOptions);
            if (parsed is null)
            {
                reason = "The message body is empty or incomplete.";
                return false;
            }

            envelope = SourceItemReadyEnvelope.Create(
                parsed.MessageId,
                parsed.SourceItemId,
                parsed.SourceId,
                parsed.IngestionRunId,
                parsed.CorrelationId,
                parsed.OccurredAtUtc,
                parsed.TraceParent);
        }
        catch (JsonException)
        {
            reason = "The message body is not valid SourceItemReady JSON.";
            return false;
        }
        catch (ArgumentException)
        {
            reason = "The message body violates the SourceItemReady contract.";
            return false;
        }

        if (!Guid.TryParse(message.MessageId, out var brokerMessageId) || brokerMessageId != envelope.MessageId)
        {
            reason = "Broker message ID does not match the stable outbox message ID.";
            return false;
        }

        if (!string.Equals(message.MessageId, envelope.MessageId.ToString("D"), StringComparison.Ordinal))
        {
            reason = "Broker message ID is not canonical GUID form.";
            return false;
        }

        if (!string.Equals(message.CorrelationId, envelope.CorrelationId.ToString("D"), StringComparison.Ordinal))
        {
            reason = "Broker correlation ID does not match the message envelope.";
            return false;
        }

        if (message.TraceParent is not null && !string.Equals(message.TraceParent, envelope.TraceParent, StringComparison.Ordinal))
        {
            reason = "Broker trace context does not match the message envelope.";
            return false;
        }

        return true;
    }

    private static Activity? StartMessageActivity(string? traceParent)
    {
        if (traceParent is not null && ActivityContext.TryParse(traceParent, traceState: null, out var parentContext))
        {
            return ActivitySource.StartActivity("SourceItemReady.Process", ActivityKind.Consumer, parentContext);
        }

        return ActivitySource.StartActivity("SourceItemReady.Process", ActivityKind.Consumer);
    }
}
