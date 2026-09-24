namespace PersonalTechBrief.Application.Analysis;

/// <summary>
/// Configuration for the grouping/relevance pipeline and its inbox-draining consumer
/// (processing pipeline §13 stages 6–8). All bounds are configuration, not scattered constants.
/// </summary>
public sealed class GroupingPipelineOptions
{
    public const string SectionName = "GroupingPipeline";

    /// <summary>Maximum pending inbox receipts drained per poll cycle.</summary>
    public int BatchSize { get; init; } = 20;

    /// <summary>Delay between poll cycles when the consumer drains the durable inbox.</summary>
    public int PollIntervalSeconds { get; init; } = 5;

    /// <summary>Recent time window (hours) that bounds grouping candidates.</summary>
    public int GroupingWindowHours { get; init; } = 168;

    /// <summary>Maximum existing updates compared for similarity per item.</summary>
    public int MaxComparisons { get; init; } = 25;

    /// <summary>Merge when the best similarity is at or above this threshold (equality merges).</summary>
    public double MergeThreshold { get; init; } = 0.78d;

    /// <summary>Bounded transient-retry budget before a receipt is escalated to a terminal failure.</summary>
    public int MaxProcessingAttempts { get; init; } = 5;
}
