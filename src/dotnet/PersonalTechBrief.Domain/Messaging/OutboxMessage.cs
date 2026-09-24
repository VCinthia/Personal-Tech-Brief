namespace PersonalTechBrief.Domain.Messaging;

public sealed class OutboxMessage
{
    public const int DestinationMaxLength = 128;
    public const int ErrorDetailMaxLength = 2_048;

    private OutboxMessage()
    {
    }

    private OutboxMessage(
        Guid id,
        Guid sourceItemId,
        Guid? sourceId,
        Guid? ingestionRunId,
        Guid correlationId,
        string destination,
        string payload,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty || sourceItemId == Guid.Empty || correlationId == Guid.Empty)
        {
            throw new ArgumentException("An outbox message requires non-empty identifiers.");
        }

        // A source-less manual article (FR-003) has no owning source or ingestion run;
        // feed items always carry both. A supplied identifier must still be non-empty.
        if (sourceId == Guid.Empty || ingestionRunId == Guid.Empty)
        {
            throw new ArgumentException("Outbox source and ingestion-run identifiers, when present, must be non-empty.");
        }

        if (string.IsNullOrWhiteSpace(destination) || destination.Trim().Length > DestinationMaxLength)
        {
            throw new ArgumentException("An outbox destination is required and must be bounded.", nameof(destination));
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("An outbox message requires a payload.", nameof(payload));
        }

        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        Id = id;
        SourceItemId = sourceItemId;
        SourceId = sourceId;
        IngestionRunId = ingestionRunId;
        CorrelationId = correlationId;
        Destination = destination.Trim();
        Payload = payload;
        CreatedAtUtc = createdAtUtc;
        Status = OutboxMessageStatus.Pending;
    }

    public Guid Id { get; private set; }

    public Guid SourceItemId { get; private set; }

    public Guid? SourceId { get; private set; }

    public Guid? IngestionRunId { get; private set; }

    public Guid CorrelationId { get; private set; }

    public string Destination { get; private set; } = null!;

    public string Payload { get; private set; } = null!;

    public OutboxMessageStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? DispatchedAtUtc { get; private set; }

    public int DispatchAttemptCount { get; private set; }

    public DateTime? LastDispatchAttemptAtUtc { get; private set; }

    public string? LastDispatchError { get; private set; }

    public static OutboxMessage CreatePending(
        Guid id,
        Guid sourceItemId,
        Guid? sourceId,
        Guid? ingestionRunId,
        Guid correlationId,
        string destination,
        string payload,
        DateTime createdAtUtc) =>
        new(id, sourceItemId, sourceId, ingestionRunId, correlationId, destination, payload, createdAtUtc);

    public void RecordDispatchFailure(string errorDetail, DateTime attemptedAtUtc)
    {
        if (Status != OutboxMessageStatus.Pending)
        {
            throw new InvalidOperationException("A terminal outbox message cannot be retried.");
        }

        if (string.IsNullOrWhiteSpace(errorDetail))
        {
            throw new ArgumentException("A dispatch failure requires diagnostics.", nameof(errorDetail));
        }

        EnsureUtc(attemptedAtUtc, nameof(attemptedAtUtc));
        DispatchAttemptCount++;
        LastDispatchAttemptAtUtc = attemptedAtUtc;
        LastDispatchError = errorDetail.Trim().Length <= ErrorDetailMaxLength
            ? errorDetail.Trim()
            : errorDetail.Trim()[..ErrorDetailMaxLength];
    }

    public void MarkDispatched(DateTime dispatchedAtUtc)
    {
        if (Status != OutboxMessageStatus.Pending)
        {
            return;
        }

        EnsureUtc(dispatchedAtUtc, nameof(dispatchedAtUtc));
        DispatchAttemptCount++;
        LastDispatchAttemptAtUtc = dispatchedAtUtc;
        LastDispatchError = null;
        DispatchedAtUtc = dispatchedAtUtc;
        Status = OutboxMessageStatus.Dispatched;
    }

    public void Quarantine(string diagnostic, DateTime attemptedAtUtc)
    {
        if (Status != OutboxMessageStatus.Pending)
        {
            return;
        }

        RecordDispatchFailure(diagnostic, attemptedAtUtc);
        Status = OutboxMessageStatus.Quarantined;
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Outbox timestamps must be UTC.", parameterName);
        }
    }
}
