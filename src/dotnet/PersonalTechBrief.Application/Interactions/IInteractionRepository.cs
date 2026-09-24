using PersonalTechBrief.Domain.Interactions;

namespace PersonalTechBrief.Application.Interactions;

/// <summary>
/// Persistence boundary for per-update interactions (FR-013 subset, data model §11). Feedback rows are
/// append-only; save is keyed by update so it is idempotent; source-open events are append-only telemetry.
/// Interactions live in their own tables keyed to the <see cref="Domain.Analysis.TechnologyUpdate"/> and
/// never mutate a historical brief snapshot.
/// </summary>
public interface IInteractionRepository
{
    /// <summary>True when a live technology update with the identifier exists.</summary>
    Task<bool> UpdateExistsAsync(Guid technologyUpdateId, CancellationToken cancellationToken);

    /// <summary>
    /// True when the brief item exists AND belongs to the given update, so optional feedback
    /// attribution cannot reference a brief item from a different update.
    /// </summary>
    Task<bool> BriefItemBelongsToUpdateAsync(
        Guid briefItemId,
        Guid technologyUpdateId,
        CancellationToken cancellationToken);

    /// <summary>
    /// True when the source item is a supporting source of the update (a <c>TechnologyUpdateSource</c> pair).
    /// The source-open guard rejects any pair that is not present.
    /// </summary>
    Task<bool> IsSupportingSourceAsync(Guid technologyUpdateId, Guid sourceItemId, CancellationToken cancellationToken);

    /// <summary>True when the update is currently saved.</summary>
    Task<bool> IsSavedAsync(Guid technologyUpdateId, CancellationToken cancellationToken);

    /// <summary>Appends a feedback row (a later row supersedes an earlier one for evaluation).</summary>
    Task AddFeedbackAsync(UserFeedback feedback, CancellationToken cancellationToken);

    /// <summary>Removes every feedback row for the update, so it has no current feedback.</summary>
    Task ClearFeedbackAsync(Guid technologyUpdateId, CancellationToken cancellationToken);

    /// <summary>Persists a saved marker for the update.</summary>
    Task AddSavedAsync(SavedUpdate saved, CancellationToken cancellationToken);

    /// <summary>Removes the saved marker for the update, if present.</summary>
    Task RemoveSavedAsync(Guid technologyUpdateId, CancellationToken cancellationToken);

    /// <summary>Appends a source-open telemetry event.</summary>
    Task AddSourceOpenAsync(SourceOpenEvent sourceOpen, CancellationToken cancellationToken);
}
