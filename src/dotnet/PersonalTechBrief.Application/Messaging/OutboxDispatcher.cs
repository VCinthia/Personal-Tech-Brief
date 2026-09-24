using System.Text.Json;

namespace PersonalTechBrief.Application.Messaging;

/// <summary>
/// Publishes durable outbox records using stable outbox identifiers. A database update
/// happens only after the broker accepts the message, so a crash after send can result
/// in a duplicate delivery but cannot silently lose a committed source item.
/// </summary>
public sealed class OutboxDispatcher(
    IOutboxMessageStore store,
    IContentProcessingPublisher publisher,
    TimeProvider timeProvider) : IOutboxDispatcher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<OutboxDispatchBatchResult> DispatchPendingAsync(
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Outbox batch size must be between 1 and 100.");
        }

        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "Outbox lease duration must be between zero and five minutes.");
        }

        var dispatchedCount = 0;
        var failedCount = 0;
        var invalidCount = 0;

        for (var index = 0; index < batchSize; index++)
        {
            var claimedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            var candidate = await store.TryClaimNextPendingAsync(
                claimedAtUtc - leaseDuration,
                claimedAtUtc,
                cancellationToken);
            if (candidate is null)
            {
                break;
            }

            if (!TryCreateEnvelope(candidate, out var envelope, out var diagnostic))
            {
                await store.QuarantineAsync(candidate.Id, diagnostic, claimedAtUtc, cancellationToken);
                invalidCount++;
                continue;
            }

            try
            {
                await publisher.PublishAsync(envelope, cancellationToken);
                await store.MarkDispatchedAsync(candidate.Id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
                dispatchedCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await store.RecordDispatchFailureAsync(
                    candidate.Id,
                    $"Broker publish failed: {exception.GetType().Name}.",
                    timeProvider.GetUtcNow().UtcDateTime,
                    cancellationToken);
                failedCount++;
            }
        }

        return new OutboxDispatchBatchResult(dispatchedCount, failedCount, invalidCount);
    }

    private static bool TryCreateEnvelope(
        PendingOutboxMessage candidate,
        out SourceItemReadyEnvelope envelope,
        out string diagnostic)
    {
        envelope = null!;
        diagnostic = string.Empty;

        if (!string.Equals(candidate.Destination, SourceItemReadyEnvelope.Destination, StringComparison.Ordinal))
        {
            diagnostic = "Outbox destination is not the approved content-processing queue.";
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<SourceItemReadyEnvelope>(candidate.Payload, SerializerOptions);
            if (parsed is null)
            {
                diagnostic = "Outbox payload does not contain a SourceItemReady envelope.";
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
            diagnostic = "Outbox payload is not valid SourceItemReady JSON.";
            return false;
        }
        catch (ArgumentException)
        {
            diagnostic = "Outbox payload violates the SourceItemReady contract.";
            return false;
        }

        if (envelope.MessageId != candidate.Id ||
            envelope.SourceItemId != candidate.SourceItemId ||
            envelope.SourceId != candidate.SourceId ||
            envelope.IngestionRunId != candidate.IngestionRunId ||
            envelope.CorrelationId != candidate.CorrelationId)
        {
            diagnostic = "Outbox metadata does not match its SourceItemReady envelope.";
            return false;
        }

        return true;
    }
}
