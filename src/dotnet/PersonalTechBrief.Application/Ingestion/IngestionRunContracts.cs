using PersonalTechBrief.Domain.Ingestion;

namespace PersonalTechBrief.Application.Ingestion;

public sealed record StartIngestionRunCommand(Guid SourceId, Guid CorrelationId, DateTime StartedAtUtc);

public sealed record StartedIngestionRun(Guid IngestionRunId, Guid SourceId, Guid CorrelationId, DateTime StartedAtUtc);

public sealed record CompleteIngestionRunCommand(
    Guid IngestionRunId,
    int RetrievedItemCount,
    int NewItemCount,
    DateTime CompletedAtUtc,
    bool NotModified,
    string? ETag,
    DateTimeOffset? LastModified);

public sealed record FailIngestionRunCommand(
    Guid IngestionRunId,
    string ErrorCode,
    string? ErrorDetail,
    DateTime CompletedAtUtc,
    int RetrievedItemCount = 0,
    int NewItemCount = 0);

public interface IIngestionRunService
{
    Task<StartedIngestionRun> StartAsync(StartIngestionRunCommand command, CancellationToken cancellationToken);

    Task CompleteAsync(CompleteIngestionRunCommand command, CancellationToken cancellationToken);

    Task FailAsync(FailIngestionRunCommand command, CancellationToken cancellationToken);
}
