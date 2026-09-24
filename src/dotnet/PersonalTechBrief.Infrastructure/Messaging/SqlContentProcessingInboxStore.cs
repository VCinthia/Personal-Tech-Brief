using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalTechBrief.Application.Messaging;
using PersonalTechBrief.Domain.Ingestion;
using PersonalTechBrief.Domain.Messaging;
using PersonalTechBrief.Infrastructure.Persistence;

namespace PersonalTechBrief.Infrastructure.Messaging;

public sealed class SqlContentProcessingInboxStore(PersonalTechBriefDbContext dbContext)
    : IContentProcessingInboxStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<ContentProcessingInboxAcceptance> AcceptAsync(
        SourceItemReadyEnvelope envelope,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken)
    {
        envelope = SourceItemReadyEnvelope.Create(
            envelope.MessageId, envelope.SourceItemId, envelope.SourceId, envelope.IngestionRunId,
            envelope.CorrelationId, envelope.OccurredAtUtc, envelope.TraceParent);
        var receipt = ContentProcessingInboxReceipt.CreatePending(
            envelope.SourceItemId, envelope.MessageId, envelope.CorrelationId, envelope.ToJson(), receivedAtUtc);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        // Serialize competing deliveries for this item before inspecting/inserting the
        // unique receipt. No SQL exception is treated as proof of duplicate acceptance.
        var sourceItem = await dbContext.SourceItems
            .FromSqlInterpolated($"SELECT * FROM [SourceItems] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {envelope.SourceItemId}")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (sourceItem is null)
        {
            return ContentProcessingInboxAcceptance.SourceItemNotFound;
        }

        var outbox = await dbContext.OutboxMessages.AsNoTracking()
            .SingleOrDefaultAsync(message => message.Id == envelope.MessageId, cancellationToken);
        if (sourceItem.SourceId != envelope.SourceId || outbox is null ||
            outbox.SourceItemId != envelope.SourceItemId || outbox.SourceId != envelope.SourceId ||
            outbox.IngestionRunId != envelope.IngestionRunId || outbox.CorrelationId != envelope.CorrelationId ||
            outbox.CreatedAtUtc != envelope.OccurredAtUtc || outbox.Status == OutboxMessageStatus.Quarantined)
        {
            return ContentProcessingInboxAcceptance.ReferenceMismatch;
        }

        if (sourceItem.ProcessingStatus is SourceItemProcessingStatus.Processed or
            SourceItemProcessingStatus.Filtered or SourceItemProcessingStatus.FailedTerminal)
        {
            return ContentProcessingInboxAcceptance.SourceItemTerminal;
        }

        if (sourceItem.ProcessingStatus is not (SourceItemProcessingStatus.Queued or
            SourceItemProcessingStatus.Processing or SourceItemProcessingStatus.FailedRetryable))
        {
            return ContentProcessingInboxAcceptance.InvalidSourceItemState;
        }

        if (await dbContext.ContentProcessingInbox.AnyAsync(
            candidate => candidate.SourceItemId == envelope.SourceItemId, cancellationToken))
        {
            return ContentProcessingInboxAcceptance.AlreadyAccepted;
        }

        dbContext.ContentProcessingInbox.Add(receipt);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ContentProcessingInboxAcceptance.Accepted;
    }

    public async Task<IReadOnlyList<PendingContentProcessingReceipt>> LoadPendingAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Inbox batch size must be between 1 and 100.");
        }

        var receipts = await dbContext.ContentProcessingInbox.AsNoTracking()
            .Where(receipt => receipt.Status == ContentProcessingInboxStatus.Pending)
            .OrderBy(receipt => receipt.ReceivedAtUtc).ThenBy(receipt => receipt.SourceItemId)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        return receipts.Select(receipt => new PendingContentProcessingReceipt(
            JsonSerializer.Deserialize<SourceItemReadyEnvelope>(receipt.EnvelopeJson, SerializerOptions)
                ?? throw new InvalidOperationException("The durable inbox envelope is missing."),
            DateTime.SpecifyKind(receipt.ReceivedAtUtc, DateTimeKind.Utc))).ToArray();
    }

    public async Task MarkProcessedAsync(Guid sourceItemId, CancellationToken cancellationToken)
    {
        var receipt = await dbContext.ContentProcessingInbox
            .SingleOrDefaultAsync(candidate => candidate.SourceItemId == sourceItemId, cancellationToken);
        if (receipt is null || receipt.Status == ContentProcessingInboxStatus.Processed)
        {
            return;
        }

        receipt.MarkProcessed();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
