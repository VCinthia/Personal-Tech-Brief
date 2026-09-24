using PersonalTechBrief.Domain.Interactions;

namespace PersonalTechBrief.Application.Interactions;

/// <summary>
/// Coordinates the FR-013 interaction subset over <see cref="IInteractionRepository"/>. All timestamps
/// come from the injected <see cref="TimeProvider"/> so behaviour is deterministic under test.
/// </summary>
public sealed class InteractionService(IInteractionRepository repository, TimeProvider timeProvider)
    : IInteractionService
{
    public async Task<RecordFeedbackOutcome> RecordFeedbackAsync(
        Guid technologyUpdateId,
        FeedbackType feedbackType,
        Guid? briefItemId,
        CancellationToken cancellationToken)
    {
        if (!await repository.UpdateExistsAsync(technologyUpdateId, cancellationToken))
        {
            return RecordFeedbackOutcome.UpdateNotFound;
        }

        if (briefItemId is { } itemId &&
            !await repository.BriefItemBelongsToUpdateAsync(itemId, technologyUpdateId, cancellationToken))
        {
            return RecordFeedbackOutcome.BriefItemNotFound;
        }

        var feedback = UserFeedback.Record(technologyUpdateId, briefItemId, feedbackType, UtcNow());
        await repository.AddFeedbackAsync(feedback, cancellationToken);
        return RecordFeedbackOutcome.Recorded;
    }

    public Task ClearFeedbackAsync(Guid technologyUpdateId, CancellationToken cancellationToken) =>
        repository.ClearFeedbackAsync(technologyUpdateId, cancellationToken);

    public async Task<bool> SaveAsync(Guid technologyUpdateId, CancellationToken cancellationToken)
    {
        if (!await repository.UpdateExistsAsync(technologyUpdateId, cancellationToken))
        {
            return false;
        }

        // Idempotent: only insert when not already saved, so re-saving is a no-op rather than a PK conflict.
        if (!await repository.IsSavedAsync(technologyUpdateId, cancellationToken))
        {
            await repository.AddSavedAsync(SavedUpdate.Create(technologyUpdateId, UtcNow()), cancellationToken);
        }

        return true;
    }

    public Task UnsaveAsync(Guid technologyUpdateId, CancellationToken cancellationToken) =>
        repository.RemoveSavedAsync(technologyUpdateId, cancellationToken);

    public async Task<bool> RecordSourceOpenAsync(
        Guid technologyUpdateId,
        Guid sourceItemId,
        CancellationToken cancellationToken)
    {
        // Telemetry is only accepted for a source item that actually supports the update.
        if (!await repository.IsSupportingSourceAsync(technologyUpdateId, sourceItemId, cancellationToken))
        {
            return false;
        }

        await repository.AddSourceOpenAsync(
            SourceOpenEvent.Record(technologyUpdateId, sourceItemId, UtcNow()),
            cancellationToken);
        return true;
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
