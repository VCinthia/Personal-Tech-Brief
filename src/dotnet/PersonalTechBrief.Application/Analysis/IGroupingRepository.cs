namespace PersonalTechBrief.Application.Analysis;

/// <summary>
/// Persistence boundary for grouping and relevance (FR-007/008/009/010/017). Implementations own the
/// transaction and make every write idempotent on the source item id, so at-least-once redelivery
/// never creates a duplicate update or association.
/// </summary>
public interface IGroupingRepository
{
    /// <summary>Loads the source item projection, or null when it no longer exists.</summary>
    Task<GroupingSourceItem?> GetSourceItemAsync(Guid sourceItemId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the bounded set of existing update representatives to compare against: those observed
    /// within the recent window and (when topics are supplied) overlapping on primary topic, newest
    /// first, capped at the configured maximum comparison count.
    /// </summary>
    Task<IReadOnlyList<GroupingCandidate>> FindCandidateUpdatesAsync(
        GroupingCandidateQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// Links the source item to an existing update or creates a new one, upserts its interest matches,
    /// then invokes <paramref name="computeScore"/> with the group-level signals and stores the score.
    /// The whole decision commits in one transaction. Idempotent: if the item is already associated,
    /// no duplicate is created and the current score is returned.
    /// </summary>
    Task<GroupingCommitResult> CommitGroupingAsync(
        CommitGroupingCommand command,
        Func<GroupScoringSignals, RelevanceScore> computeScore,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a processing failure for the bounded-retry policy. A retryable failure keeps the item
    /// eligible for another attempt; a terminal failure stops the loop. Returns the updated failure
    /// count. Idempotent no-op once the item has reached a terminal state.
    /// </summary>
    Task<int> RecordProcessingFailureAsync(
        Guid sourceItemId,
        string failureCode,
        bool terminal,
        CancellationToken cancellationToken);
}
