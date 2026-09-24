namespace PersonalTechBrief.Domain.Analysis;

/// <summary>
/// Many-to-many association linking a supporting <see cref="TechnologyUpdate"/> to a source item
/// (FR-017 source attribution). The unique <c>(TechnologyUpdateId, SourceItemId)</c> pair plus a
/// check-before-insert makes re-linking a no-op under at-least-once redelivery.
/// </summary>
public sealed class TechnologyUpdateSource
{
    private TechnologyUpdateSource()
    {
    }

    private TechnologyUpdateSource(
        Guid technologyUpdateId,
        Guid sourceItemId,
        double? similarityScore,
        DateTime linkedAtUtc)
    {
        if (technologyUpdateId == Guid.Empty)
        {
            throw new ArgumentException("A source association requires a technology update identifier.", nameof(technologyUpdateId));
        }

        if (sourceItemId == Guid.Empty)
        {
            throw new ArgumentException("A source association requires a source item identifier.", nameof(sourceItemId));
        }

        if (similarityScore is { } score && (score < 0d || score > 1d || double.IsNaN(score)))
        {
            throw new ArgumentOutOfRangeException(nameof(similarityScore), "A similarity score must be within [0, 1].");
        }

        if (linkedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A source association timestamp must be UTC.", nameof(linkedAtUtc));
        }

        TechnologyUpdateId = technologyUpdateId;
        SourceItemId = sourceItemId;
        SimilarityScore = similarityScore;
        LinkedAtUtc = linkedAtUtc;
    }

    public Guid TechnologyUpdateId { get; private set; }

    public Guid SourceItemId { get; private set; }

    /// <summary>Similarity to the group representative at link time; null for the founding item.</summary>
    public double? SimilarityScore { get; private set; }

    public DateTime LinkedAtUtc { get; private set; }

    public static TechnologyUpdateSource Link(
        Guid technologyUpdateId,
        Guid sourceItemId,
        double? similarityScore,
        DateTime linkedAtUtc) =>
        new(technologyUpdateId, sourceItemId, similarityScore, linkedAtUtc);
}
