using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Ingestion;
using PersonalTechBrief.Application.Messaging;
using PersonalTechBrief.Domain.Ingestion;
using PersonalTechBrief.Domain.Messaging;

namespace PersonalTechBrief.Infrastructure.Persistence;

/// <summary>
/// SQL-backed implementation of the deterministic duplicate boundary. The serializable
/// transaction covers both lookup and persistence so a duplicate can never create a second outbox row.
/// </summary>
public sealed class SqlSourceItemPersistenceService(
    PersonalTechBriefDbContext dbContext,
    IOptions<IngestionDeduplicationOptions> options) : ISourceItemPersistenceService
{
    private readonly int titleWindowDays = options.Value.TitleWindowDays;

    public async Task<PersistSourceItemResult> PersistIfNewAsync(
        PersistSourceItemCommand command,
        CancellationToken cancellationToken)
    {
        var item = SourceItem.CreateFeed(
            command.SourceId,
            command.ExternalId,
            command.SourceUrl,
            command.Title,
            command.Excerpt,
            command.PublishedAtUtc,
            command.ContentHash,
            command.RetrievedAtUtc);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        OutboxMessage? outboxMessage = null;
        try
        {
            await EnsureKnownSourceAndRunAsync(command.SourceId, command.IngestionRunId, cancellationToken);
            if (await HasDuplicateAsync(item, cancellationToken))
            {
                await transaction.CommitAsync(cancellationToken);
                return PersistSourceItemResult.Duplicate();
            }

            var outboxMessageId = Guid.NewGuid();
            var envelope = SourceItemReadyEnvelope.Create(
                outboxMessageId,
                item.Id,
                command.SourceId,
                command.IngestionRunId,
                command.CorrelationId,
                command.RetrievedAtUtc,
                command.TraceParent);
            outboxMessage = OutboxMessage.CreatePending(
                outboxMessageId,
                item.Id,
                command.SourceId,
                command.IngestionRunId,
                command.CorrelationId,
                SourceItemReadyEnvelope.Destination,
                envelope.ToJson(),
                command.RetrievedAtUtc);

            dbContext.SourceItems.Add(item);
            dbContext.OutboxMessages.Add(outboxMessage);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return PersistSourceItemResult.Persisted(item.Id, outboxMessage.Id);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return PersistSourceItemResult.Duplicate();
        }
        finally
        {
            // The transaction owns only this item/outbox pair. Remove it from tracking on
            // every outcome so a failed write cannot leak into later items or run updates.
            dbContext.Entry(item).State = EntityState.Detached;
            if (outboxMessage is not null)
            {
                dbContext.Entry(outboxMessage).State = EntityState.Detached;
            }
        }
    }

    private async Task EnsureKnownSourceAndRunAsync(Guid sourceId, Guid ingestionRunId, CancellationToken cancellationToken)
    {
        if (sourceId == Guid.Empty || ingestionRunId == Guid.Empty)
        {
            throw new ArgumentException("Source and ingestion run identifiers are required.");
        }

        var runBelongsToSource = await dbContext.IngestionRuns.AnyAsync(
            run => run.Id == ingestionRunId && run.SourceId == sourceId,
            cancellationToken);
        if (!runBelongsToSource)
        {
            throw new KeyNotFoundException("The requested ingestion run does not belong to the source.");
        }
    }

    private async Task<bool> HasDuplicateAsync(SourceItem item, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(item.ExternalId) &&
            await dbContext.SourceItems.AnyAsync(
                existing => existing.SourceId == item.SourceId && existing.ExternalId == item.ExternalId,
                cancellationToken))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(item.NormalizedUrl) &&
            await dbContext.SourceItems.AnyAsync(
                existing => existing.NormalizedUrlHash == item.NormalizedUrlHash &&
                    existing.NormalizedUrl == item.NormalizedUrl,
                cancellationToken))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(item.ContentHash) &&
            await dbContext.SourceItems.AnyAsync(
                existing => existing.ContentHash == item.ContentHash,
                cancellationToken))
        {
            return true;
        }

        if (item.ExternalId is not null || item.NormalizedUrl is not null || item.ContentHash is not null)
        {
            return false;
        }

        var titleWindowStart = item.RetrievedAtUtc.AddDays(-titleWindowDays);
        return await dbContext.SourceItems.AnyAsync(
            existing => existing.NormalizedTitle == item.NormalizedTitle &&
                existing.RetrievedAtUtc >= titleWindowStart &&
                existing.RetrievedAtUtc <= item.RetrievedAtUtc,
            cancellationToken);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
