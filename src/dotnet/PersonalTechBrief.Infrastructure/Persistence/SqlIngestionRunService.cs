using Microsoft.EntityFrameworkCore;
using PersonalTechBrief.Application.Ingestion;
using PersonalTechBrief.Domain.Ingestion;

namespace PersonalTechBrief.Infrastructure.Persistence;

public sealed class SqlIngestionRunService(PersonalTechBriefDbContext dbContext) : IIngestionRunService
{
    public async Task<StartedIngestionRun> StartAsync(
        StartIngestionRunCommand command,
        CancellationToken cancellationToken)
    {
        var source = await dbContext.Sources.SingleOrDefaultAsync(
            candidate => candidate.Id == command.SourceId,
            cancellationToken)
            ?? throw new KeyNotFoundException("The requested source does not exist.");

        var run = IngestionRun.Start(source.Id, command.CorrelationId, command.StartedAtUtc);
        dbContext.IngestionRuns.Add(run);
        await SaveRunAndSourceAsync(run, source, cancellationToken);
        return new StartedIngestionRun(run.Id, run.SourceId, run.CorrelationId, run.StartedAtUtc);
    }

    public async Task CompleteAsync(CompleteIngestionRunCommand command, CancellationToken cancellationToken)
    {
        var (run, source) = await LoadRunAndSourceAsync(command.IngestionRunId, cancellationToken);
        run.CompleteSuccessfully(
            command.RetrievedItemCount,
            command.NewItemCount,
            command.CompletedAtUtc,
            command.NotModified);
        source.RecordIngestionOutcome(
            run.Status,
            command.CompletedAtUtc,
            command.NotModified ? command.ETag ?? source.ETag : command.ETag,
            command.NotModified ? command.LastModified ?? source.LastModified : command.LastModified);
        await SaveRunAndSourceAsync(run, source, cancellationToken);
    }

    public async Task FailAsync(FailIngestionRunCommand command, CancellationToken cancellationToken)
    {
        var (run, source) = await LoadRunAndSourceAsync(command.IngestionRunId, cancellationToken);
        run.Fail(
            command.ErrorCode,
            command.ErrorDetail,
            command.CompletedAtUtc,
            command.RetrievedItemCount,
            command.NewItemCount);
        source.RecordIngestionOutcome(run.Status, command.CompletedAtUtc, source.ETag, source.LastModified);
        await SaveRunAndSourceAsync(run, source, cancellationToken);
    }

    private async Task SaveRunAndSourceAsync(
        IngestionRun run,
        Domain.Sources.Source source,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // A failed SaveChanges leaves in-memory status/validators mutated. The next
            // operation must reload the committed state and cannot flush this failed attempt.
            dbContext.Entry(run).State = EntityState.Detached;
            dbContext.Entry(source).State = EntityState.Detached;
            throw;
        }
    }

    private async Task<(IngestionRun Run, Domain.Sources.Source Source)> LoadRunAndSourceAsync(
        Guid ingestionRunId,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.IngestionRuns.SingleOrDefaultAsync(
            candidate => candidate.Id == ingestionRunId,
            cancellationToken)
            ?? throw new KeyNotFoundException("The requested ingestion run does not exist.");
        var source = await dbContext.Sources.SingleOrDefaultAsync(
            candidate => candidate.Id == run.SourceId,
            cancellationToken)
            ?? throw new InvalidOperationException("An ingestion run references a missing source.");
        return (run, source);
    }
}
