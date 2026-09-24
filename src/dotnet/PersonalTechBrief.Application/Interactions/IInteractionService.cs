using PersonalTechBrief.Domain.Interactions;

namespace PersonalTechBrief.Application.Interactions;

/// <summary>Outcome of recording relevance feedback for an update.</summary>
public enum RecordFeedbackOutcome
{
    Recorded,
    UpdateNotFound,
    BriefItemNotFound,
}

/// <summary>
/// Application service for the FR-013 interaction subset: relevance feedback, saved state, and the
/// source-open telemetry signal. Read/Pending and Dismiss are out of scope for this slice.
/// </summary>
public interface IInteractionService
{
    /// <summary>
    /// Appends relevance feedback for the update; the latest row supersedes for evaluation (AC-012).
    /// Returns <see cref="RecordFeedbackOutcome.UpdateNotFound"/> when the update does not exist, or
    /// <see cref="RecordFeedbackOutcome.BriefItemNotFound"/> when a supplied brief item does not exist.
    /// </summary>
    Task<RecordFeedbackOutcome> RecordFeedbackAsync(
        Guid technologyUpdateId,
        FeedbackType feedbackType,
        Guid? briefItemId,
        CancellationToken cancellationToken);

    /// <summary>Clears the update's current feedback (idempotent; represents "no current feedback").</summary>
    Task ClearFeedbackAsync(Guid technologyUpdateId, CancellationToken cancellationToken);

    /// <summary>Idempotently saves the update. Returns false when the update does not exist.</summary>
    Task<bool> SaveAsync(Guid technologyUpdateId, CancellationToken cancellationToken);

    /// <summary>Idempotently unsaves the update (a no-op when it was not saved).</summary>
    Task UnsaveAsync(Guid technologyUpdateId, CancellationToken cancellationToken);

    /// <summary>
    /// Records a source-open telemetry event. Returns false when the source item is not a supporting
    /// source of the update, so the endpoint can answer 404 for a non-supporting or missing pair.
    /// </summary>
    Task<bool> RecordSourceOpenAsync(
        Guid technologyUpdateId,
        Guid sourceItemId,
        CancellationToken cancellationToken);
}
