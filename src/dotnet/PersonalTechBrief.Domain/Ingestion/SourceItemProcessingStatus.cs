namespace PersonalTechBrief.Domain.Ingestion;

public enum SourceItemProcessingStatus
{
    Pending,
    Queued,
    Processing,
    Processed,
    Filtered,
    FailedRetryable,
    FailedTerminal,
}
