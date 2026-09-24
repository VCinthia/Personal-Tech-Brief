namespace PersonalTechBrief.Domain.Ingestion;

public sealed class IngestionRun
{
    public const int ErrorCodeMaxLength = 128;
    public const int ErrorDetailMaxLength = 2_048;

    private IngestionRun()
    {
    }

    private IngestionRun(Guid sourceId, Guid correlationId, DateTime startedAtUtc)
    {
        if (sourceId == Guid.Empty)
        {
            throw new ArgumentException("An ingestion run requires a source identifier.", nameof(sourceId));
        }

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("An ingestion run requires a correlation identifier.", nameof(correlationId));
        }

        EnsureUtc(startedAtUtc, nameof(startedAtUtc));
        Id = Guid.NewGuid();
        SourceId = sourceId;
        CorrelationId = correlationId;
        StartedAtUtc = startedAtUtc;
        Status = IngestionRunStatus.Running;
    }

    public Guid Id { get; private set; }

    public Guid SourceId { get; private set; }

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public IngestionRunStatus Status { get; private set; }

    public int RetrievedItemCount { get; private set; }

    public int NewItemCount { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorDetail { get; private set; }

    public Guid CorrelationId { get; private set; }

    public static IngestionRun Start(Guid sourceId, Guid correlationId, DateTime startedAtUtc) =>
        new(sourceId, correlationId, startedAtUtc);

    public void CompleteSuccessfully(
        int retrievedItemCount,
        int newItemCount,
        DateTime completedAtUtc,
        bool notModified = false)
    {
        if (retrievedItemCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retrievedItemCount));
        }

        if (newItemCount < 0 || newItemCount > retrievedItemCount)
        {
            throw new ArgumentOutOfRangeException(nameof(newItemCount));
        }

        if (notModified && (retrievedItemCount != 0 || newItemCount != 0))
        {
            throw new ArgumentException("A not-modified run cannot report retrieved or new items.");
        }

        Complete(notModified ? IngestionRunStatus.NotModified : IngestionRunStatus.Succeeded, completedAtUtc);
        RetrievedItemCount = retrievedItemCount;
        NewItemCount = newItemCount;
    }

    public void Fail(
        string errorCode,
        string? errorDetail,
        DateTime completedAtUtc,
        int retrievedItemCount = 0,
        int newItemCount = 0)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            throw new ArgumentException("An ingestion failure requires an error code.", nameof(errorCode));
        }

        if (retrievedItemCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retrievedItemCount));
        }

        if (newItemCount < 0 || newItemCount > retrievedItemCount)
        {
            throw new ArgumentOutOfRangeException(nameof(newItemCount));
        }

        Complete(IngestionRunStatus.Failed, completedAtUtc);
        RetrievedItemCount = retrievedItemCount;
        NewItemCount = newItemCount;
        ErrorCode = Truncate(errorCode.Trim(), ErrorCodeMaxLength);
        ErrorDetail = string.IsNullOrWhiteSpace(errorDetail)
            ? null
            : Truncate(errorDetail.Trim(), ErrorDetailMaxLength);
    }

    private void Complete(IngestionRunStatus status, DateTime completedAtUtc)
    {
        if (Status != IngestionRunStatus.Running)
        {
            throw new InvalidOperationException("An ingestion run can only be completed once.");
        }

        EnsureUtc(completedAtUtc, nameof(completedAtUtc));
        if (completedAtUtc < StartedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(completedAtUtc), "Completion cannot precede start.");
        }

        Status = status;
        CompletedAtUtc = completedAtUtc;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Ingestion timestamps must be UTC.", parameterName);
        }
    }
}
