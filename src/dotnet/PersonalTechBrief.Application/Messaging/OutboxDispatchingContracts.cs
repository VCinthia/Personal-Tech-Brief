namespace PersonalTechBrief.Application.Messaging;

public sealed record PendingOutboxMessage(
    Guid Id,
    Guid SourceItemId,
    Guid? SourceId,
    Guid? IngestionRunId,
    Guid CorrelationId,
    string Destination,
    string Payload);

public interface IOutboxMessageStore
{
    /// <summary>
    /// Atomically claims one eligible pending row. A claim is a short lease represented
    /// by its last-attempt timestamp, which prevents parallel dispatcher instances from
    /// sending the same row concurrently without marking it dispatched before send.
    /// </summary>
    Task<PendingOutboxMessage?> TryClaimNextPendingAsync(
        DateTime retryEligibleBeforeUtc,
        DateTime claimedAtUtc,
        CancellationToken cancellationToken);

    Task MarkDispatchedAsync(Guid outboxMessageId, DateTime dispatchedAtUtc, CancellationToken cancellationToken);

    Task RecordDispatchFailureAsync(
        Guid outboxMessageId,
        string diagnostic,
        DateTime attemptedAtUtc,
        CancellationToken cancellationToken);

    Task QuarantineAsync(
        Guid outboxMessageId,
        string diagnostic,
        DateTime attemptedAtUtc,
        CancellationToken cancellationToken);
}

public sealed class OutboxDispatchOptions
{
    public const string SectionName = "OutboxDispatch";

    public int BatchSize { get; init; } = 20;

    public int PollIntervalSeconds { get; init; } = 5;

    public int LeaseDurationSeconds { get; init; } = 30;
}

public sealed record OutboxDispatchBatchResult(int DispatchedCount, int FailedCount, int InvalidCount)
{
    public bool HasActivity => DispatchedCount > 0 || FailedCount > 0 || InvalidCount > 0;
}

public interface IOutboxDispatcher
{
    Task<OutboxDispatchBatchResult> DispatchPendingAsync(
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);
}
