namespace PersonalTechBrief.Domain.Briefs;

/// <summary>
/// An immutable snapshot of one selected update at brief-generation time (data model §11). Its
/// title/summary/why-relevant text, relevance score, and generation metadata capture the values at
/// generation time and are never rewritten. Each item retains at least one supporting source
/// reference (FR-012/AC-010) surfaced with a way to open the original source.
/// </summary>
public sealed class BriefItem
{
    public const int TitleSnapshotMaxLength = 512;
    public const int TopicSnapshotMaxLength = 256;
    public const int SummarySnapshotMaxLength = 4000;
    public const int WhyRelevantSnapshotMaxLength = 4000;
    public const int VersionMaxLength = 64;

    private readonly List<BriefItemSource> _sources = [];

    private BriefItem()
    {
    }

    private BriefItem(
        Guid id,
        Guid briefId,
        Guid technologyUpdateId,
        int rank,
        string titleSnapshot,
        string topicSnapshot,
        string summarySnapshot,
        string whyRelevantSnapshot,
        double relevanceScoreSnapshot,
        DateTime generatedAtUtc,
        string promptVersion,
        string modelOrAlgorithmVersion,
        IReadOnlyList<BriefItemSourceSnapshot> sources)
    {
        if (briefId == Guid.Empty)
        {
            throw new ArgumentException("A brief item requires a brief identifier.", nameof(briefId));
        }

        if (technologyUpdateId == Guid.Empty)
        {
            throw new ArgumentException("A brief item requires a technology update identifier.", nameof(technologyUpdateId));
        }

        if (rank < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), "A brief item rank must be positive.");
        }

        if (double.IsNaN(relevanceScoreSnapshot) || double.IsInfinity(relevanceScoreSnapshot))
        {
            throw new ArgumentOutOfRangeException(nameof(relevanceScoreSnapshot), "A relevance score snapshot must be finite.");
        }

        EnsureUtc(generatedAtUtc, nameof(generatedAtUtc));
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count == 0)
        {
            throw new ArgumentException("A brief item must retain at least one supporting source reference.", nameof(sources));
        }

        Id = id;
        BriefId = briefId;
        TechnologyUpdateId = technologyUpdateId;
        Rank = rank;
        TitleSnapshot = CleanRequiredText(titleSnapshot, TitleSnapshotMaxLength, nameof(titleSnapshot));
        TopicSnapshot = CleanRequiredText(topicSnapshot, TopicSnapshotMaxLength, nameof(topicSnapshot));
        SummarySnapshot = CleanRequiredText(summarySnapshot, SummarySnapshotMaxLength, nameof(summarySnapshot));
        WhyRelevantSnapshot = CleanRequiredText(whyRelevantSnapshot, WhyRelevantSnapshotMaxLength, nameof(whyRelevantSnapshot));
        RelevanceScoreSnapshot = relevanceScoreSnapshot;
        GeneratedAtUtc = generatedAtUtc;
        PromptVersion = CleanRequiredText(promptVersion, VersionMaxLength, nameof(promptVersion));
        ModelOrAlgorithmVersion = CleanRequiredText(modelOrAlgorithmVersion, VersionMaxLength, nameof(modelOrAlgorithmVersion));

        foreach (var source in sources)
        {
            _sources.Add(BriefItemSource.Create(Id, source.SourceItemId, source.TitleSnapshot, source.UrlSnapshot, source.PublishedAtUtc));
        }
    }

    public Guid Id { get; private set; }

    public Guid BriefId { get; private set; }

    public Guid TechnologyUpdateId { get; private set; }

    public int Rank { get; private set; }

    public string TitleSnapshot { get; private set; } = null!;

    public string TopicSnapshot { get; private set; } = null!;

    public string SummarySnapshot { get; private set; } = null!;

    public string WhyRelevantSnapshot { get; private set; } = null!;

    public double RelevanceScoreSnapshot { get; private set; }

    public DateTime GeneratedAtUtc { get; private set; }

    public string PromptVersion { get; private set; } = null!;

    public string ModelOrAlgorithmVersion { get; private set; } = null!;

    public IReadOnlyList<BriefItemSource> Sources => _sources;

    public static BriefItem Create(
        Guid briefId,
        Guid technologyUpdateId,
        int rank,
        string titleSnapshot,
        string topicSnapshot,
        string summarySnapshot,
        string whyRelevantSnapshot,
        double relevanceScoreSnapshot,
        DateTime generatedAtUtc,
        string promptVersion,
        string modelOrAlgorithmVersion,
        IReadOnlyList<BriefItemSourceSnapshot> sources) =>
        new(
            Guid.NewGuid(),
            briefId,
            technologyUpdateId,
            rank,
            titleSnapshot,
            topicSnapshot,
            summarySnapshot,
            whyRelevantSnapshot,
            relevanceScoreSnapshot,
            generatedAtUtc,
            promptVersion,
            modelOrAlgorithmVersion,
            sources);

    private static string CleanRequiredText(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value is required.", parameterName);
        }

        var cleaned = value.Trim();
        return cleaned.Length > maxLength ? cleaned[..maxLength] : cleaned;
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Brief item timestamps must be UTC.", parameterName);
        }
    }
}
