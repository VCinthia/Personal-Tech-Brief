using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Articles;
using PersonalTechBrief.Application.Ingestion;
using PersonalTechBrief.Application.Messaging;
using PersonalTechBrief.Domain.Ingestion;
using PersonalTechBrief.Domain.Messaging;
using PersonalTechBrief.Infrastructure.Persistence;

namespace PersonalTechBrief.Infrastructure.Articles;

/// <summary>
/// SQL-backed persistence for a source-less manual article. The serializable transaction covers
/// both the duplicate lookup and the write so a duplicate can never create a second outbox row.
/// It reuses the deterministic duplicate precedence (normalized URL, content hash, title window)
/// but never touches Sources or IngestionRuns and never runs the source/external-id rule.
/// </summary>
public sealed class SqlManualArticleStore(
    PersonalTechBriefDbContext dbContext,
    IOptions<IngestionDeduplicationOptions> options) : IManualArticleStore
{
    private readonly int titleWindowDays = options.Value.TitleWindowDays;

    public async Task<PersistManualArticleResult> PersistIfNewAsync(
        PersistManualArticleCommand command,
        CancellationToken cancellationToken)
    {
        var item = SourceItem.CreateManual(
            command.ArticleUrl,
            command.Title,
            command.Excerpt,
            command.ContentHash,
            command.RetrievedAtUtc);

        // Read-committed is sufficient: the filtered unique index on the manual normalized-URL
        // hash is the authoritative dedup key, so a concurrent resubmission blocks on the index
        // and then fails the insert (caught below as a duplicate) rather than deadlocking on
        // serializable range locks. The pre-insert lookup is a best-effort fast path.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        OutboxMessage? outboxMessage = null;
        try
        {
            var duplicateId = await FindDuplicateIdAsync(item, cancellationToken);
            if (duplicateId is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return PersistManualArticleResult.Duplicate(duplicateId);
            }

            var outboxMessageId = Guid.NewGuid();
            var envelope = SourceItemReadyEnvelope.Create(
                outboxMessageId,
                item.Id,
                sourceId: null,
                ingestionRunId: null,
                command.CorrelationId,
                command.RetrievedAtUtc,
                command.TraceParent);
            outboxMessage = OutboxMessage.CreatePending(
                outboxMessageId,
                item.Id,
                sourceId: null,
                ingestionRunId: null,
                command.CorrelationId,
                SourceItemReadyEnvelope.Destination,
                envelope.ToJson(),
                command.RetrievedAtUtc);

            dbContext.SourceItems.Add(item);
            dbContext.OutboxMessages.Add(outboxMessage);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return PersistManualArticleResult.Persisted(item.Id, outboxMessage.Id);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return PersistManualArticleResult.Duplicate(await ResolveExistingIdAsync(item, CancellationToken.None));
        }
        finally
        {
            dbContext.Entry(item).State = EntityState.Detached;
            if (outboxMessage is not null)
            {
                dbContext.Entry(outboxMessage).State = EntityState.Detached;
            }
        }
    }

    private async Task<Guid?> FindDuplicateIdAsync(SourceItem item, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(item.NormalizedUrl))
        {
            var byUrl = await dbContext.SourceItems.AsNoTracking()
                .Where(existing => existing.NormalizedUrlHash == item.NormalizedUrlHash &&
                    existing.NormalizedUrl == item.NormalizedUrl)
                .Select(existing => (Guid?)existing.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (byUrl is not null)
            {
                return byUrl;
            }
        }

        if (!string.IsNullOrWhiteSpace(item.ContentHash))
        {
            var byHash = await dbContext.SourceItems.AsNoTracking()
                .Where(existing => existing.ContentHash == item.ContentHash)
                .Select(existing => (Guid?)existing.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (byHash is not null)
            {
                return byHash;
            }
        }

        if (item.NormalizedUrl is not null || item.ContentHash is not null)
        {
            return null;
        }

        var titleWindowStart = item.RetrievedAtUtc.AddDays(-titleWindowDays);
        return await dbContext.SourceItems.AsNoTracking()
            .Where(existing => existing.NormalizedTitle == item.NormalizedTitle &&
                existing.RetrievedAtUtc >= titleWindowStart &&
                existing.RetrievedAtUtc <= item.RetrievedAtUtc)
            .Select(existing => (Guid?)existing.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveExistingIdAsync(SourceItem item, CancellationToken cancellationToken) =>
        await FindDuplicateIdAsync(item, cancellationToken);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
