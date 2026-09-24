using PersonalTechBrief.Application.Messaging;

namespace PersonalTechBrief.Application.Analysis;

/// <summary>The terminal disposition of one pipeline run for a single inbox receipt.</summary>
public enum GroupingProcessingOutcome
{
    /// <summary>The item was analyzed, grouped, scored, and the receipt completed.</summary>
    Grouped,

    /// <summary>The item was already handled (terminal state or already grouped); receipt completed idempotently.</summary>
    SkippedAlreadyHandled,

    /// <summary>The referenced source item no longer exists; the receipt was completed.</summary>
    SkippedItemMissing,

    /// <summary>A transient dependency failure occurred; the receipt stays pending for a bounded retry.</summary>
    RetryScheduled,

    /// <summary>A terminal/validation failure occurred; the item and receipt were closed to stop the loop.</summary>
    FailedTerminal,
}

/// <summary>
/// Runs grouping and relevance (pipeline §13 stages 6–8) for one durable inbox receipt: analyze,
/// bound candidates, similarity, link/create, score, persist, then mark the receipt processed.
/// Idempotent under at-least-once redelivery; analyzes each source item at most once.
/// </summary>
public interface IGroupingPipelineService
{
    Task<GroupingProcessingOutcome> ProcessAsync(SourceItemReadyEnvelope envelope, CancellationToken cancellationToken);
}
