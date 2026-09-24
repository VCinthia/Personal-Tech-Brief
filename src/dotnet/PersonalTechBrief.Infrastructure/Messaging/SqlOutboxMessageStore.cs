using Microsoft.EntityFrameworkCore;
using PersonalTechBrief.Application.Messaging;
using PersonalTechBrief.Domain.Messaging;
using PersonalTechBrief.Infrastructure.Persistence;

namespace PersonalTechBrief.Infrastructure.Messaging;

public sealed class SqlOutboxMessageStore(PersonalTechBriefDbContext dbContext) : IOutboxMessageStore
{
    public async Task<PendingOutboxMessage?> TryClaimNextPendingAsync(
        DateTime retryEligibleBeforeUtc,
        DateTime claimedAtUtc,
        CancellationToken cancellationToken)
    {
        EnsureUtc(retryEligibleBeforeUtc, nameof(retryEligibleBeforeUtc));
        EnsureUtc(claimedAtUtc, nameof(claimedAtUtc));

        var candidates = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.Status == OutboxMessageStatus.Pending &&
                (message.LastDispatchAttemptAtUtc == null ||
                 message.LastDispatchAttemptAtUtc <= retryEligibleBeforeUtc))
            .OrderBy(message => message.CreatedAtUtc)
            .ThenBy(message => message.Id)
            .Select(message => new PendingOutboxMessage(
                message.Id,
                message.SourceItemId,
                message.SourceId,
                message.IngestionRunId,
                message.CorrelationId,
                message.Destination,
                message.Payload))
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            var claims = await dbContext.OutboxMessages
                .Where(message =>
                    message.Id == candidate.Id &&
                    message.Status == OutboxMessageStatus.Pending &&
                    (message.LastDispatchAttemptAtUtc == null ||
                     message.LastDispatchAttemptAtUtc <= retryEligibleBeforeUtc))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(message => message.LastDispatchAttemptAtUtc, claimedAtUtc),
                    cancellationToken);
            if (claims == 1)
            {
                return candidate;
            }
        }

        return null;
    }

    public async Task MarkDispatchedAsync(
        Guid outboxMessageId,
        DateTime dispatchedAtUtc,
        CancellationToken cancellationToken)
    {
        EnsureUtc(dispatchedAtUtc, nameof(dispatchedAtUtc));
        var message = await dbContext.OutboxMessages.SingleOrDefaultAsync(
            candidate => candidate.Id == outboxMessageId,
            cancellationToken);
        if (message is null || message.Status == OutboxMessageStatus.Dispatched)
        {
            return;
        }

        message.MarkDispatched(dispatchedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordDispatchFailureAsync(
        Guid outboxMessageId,
        string diagnostic,
        DateTime attemptedAtUtc,
        CancellationToken cancellationToken)
    {
        EnsureUtc(attemptedAtUtc, nameof(attemptedAtUtc));
        var message = await dbContext.OutboxMessages.SingleOrDefaultAsync(
            candidate => candidate.Id == outboxMessageId,
            cancellationToken);
        if (message is null || message.Status == OutboxMessageStatus.Dispatched)
        {
            return;
        }

        message.RecordDispatchFailure(diagnostic, attemptedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task QuarantineAsync(
        Guid outboxMessageId,
        string diagnostic,
        DateTime attemptedAtUtc,
        CancellationToken cancellationToken)
    {
        EnsureUtc(attemptedAtUtc, nameof(attemptedAtUtc));
        var message = await dbContext.OutboxMessages.SingleOrDefaultAsync(
            candidate => candidate.Id == outboxMessageId,
            cancellationToken);
        if (message is null || message.Status != OutboxMessageStatus.Pending)
        {
            return;
        }

        message.Quarantine(diagnostic, attemptedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Outbox timestamps must be UTC.", parameterName);
        }
    }
}
