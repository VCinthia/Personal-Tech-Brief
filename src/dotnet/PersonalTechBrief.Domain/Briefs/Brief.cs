namespace PersonalTechBrief.Domain.Briefs;

/// <summary>
/// A generated brief: the immutable historical snapshot of the updates a user was shown at a point
/// in time (FR-011/014, data model §11). A brief is created in <see cref="BriefStatus.Generating"/>
/// and transitions exactly once to <see cref="BriefStatus.Completed"/> (possibly with zero items) or
/// <see cref="BriefStatus.Failed"/>. Once it leaves <see cref="BriefStatus.Generating"/> it is
/// write-once: re-running grouping/scoring updates live <c>TechnologyUpdate</c>s only and never
/// rewrites a historical brief.
/// </summary>
public sealed class Brief
{
    public const int GenerationVersionMaxLength = 64;

    private readonly List<BriefItem> _items = [];

    private Brief()
    {
    }

    private Brief(
        Guid id,
        DateTime generatedAtUtc,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        string generationVersion,
        Guid correlationId)
    {
        EnsureUtc(generatedAtUtc, nameof(generatedAtUtc));
        EnsureUtc(windowStartUtc, nameof(windowStartUtc));
        EnsureUtc(windowEndUtc, nameof(windowEndUtc));

        if (windowEndUtc < windowStartUtc)
        {
            throw new ArgumentException("A brief window end must not precede its start.", nameof(windowEndUtc));
        }

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("A brief requires a correlation identifier.", nameof(correlationId));
        }

        Id = id;
        GeneratedAtUtc = generatedAtUtc;
        WindowStartUtc = windowStartUtc;
        WindowEndUtc = windowEndUtc;
        Status = BriefStatus.Generating;
        CandidateCount = 0;
        SelectedCount = 0;
        GenerationVersion = CleanRequiredText(generationVersion, GenerationVersionMaxLength, nameof(generationVersion));
        CorrelationId = correlationId;
    }

    public Guid Id { get; private set; }

    /// <summary>When the brief was created; the next brief's candidate window starts here (§20).</summary>
    public DateTime GeneratedAtUtc { get; private set; }

    public DateTime WindowStartUtc { get; private set; }

    public DateTime WindowEndUtc { get; private set; }

    public BriefStatus Status { get; private set; }

    /// <summary>Number of eligible candidates found in the window before the max-visible cap.</summary>
    public int CandidateCount { get; private set; }

    /// <summary>Number of items actually persisted in the completed brief (may be fewer than selected, or zero).</summary>
    public int SelectedCount { get; private set; }

    public string GenerationVersion { get; private set; } = null!;

    public Guid CorrelationId { get; private set; }

    public IReadOnlyList<BriefItem> Items => _items;

    public static Brief Create(
        DateTime generatedAtUtc,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        string generationVersion,
        Guid correlationId) =>
        new(Guid.NewGuid(), generatedAtUtc, windowStartUtc, windowEndUtc, generationVersion, correlationId);

    /// <summary>
    /// Completes the brief with the successfully generated items in rank order. Valid with zero items
    /// (all candidates excluded, or none qualified). Write-once: only permitted from
    /// <see cref="BriefStatus.Generating"/>.
    /// </summary>
    public void Complete(int candidateCount, IEnumerable<BriefItem> items)
    {
        EnsureGenerating();
        ArgumentNullException.ThrowIfNull(items);

        if (candidateCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(candidateCount), "The candidate count must not be negative.");
        }

        var materialized = items.ToList();
        var seenRanks = new HashSet<int>();
        var seenUpdates = new HashSet<Guid>();
        foreach (var item in materialized)
        {
            if (item.BriefId != Id)
            {
                throw new ArgumentException("A brief item must reference this brief.", nameof(items));
            }

            if (!seenRanks.Add(item.Rank))
            {
                throw new ArgumentException($"Duplicate brief item rank {item.Rank}.", nameof(items));
            }

            if (!seenUpdates.Add(item.TechnologyUpdateId))
            {
                throw new ArgumentException("A technology update may appear at most once in a brief.", nameof(items));
            }
        }

        _items.Clear();
        _items.AddRange(materialized);
        CandidateCount = candidateCount;
        SelectedCount = _items.Count;
        Status = BriefStatus.Completed;
    }

    /// <summary>
    /// Marks the brief failed when generation could not run to completion (e.g. candidate loading or a
    /// persistence fault). Per-item generation failures do not fail the brief; they are excluded and the
    /// brief still completes. Write-once: only permitted from <see cref="BriefStatus.Generating"/>.
    /// </summary>
    public void Fail()
    {
        EnsureGenerating();
        SelectedCount = 0;
        Status = BriefStatus.Failed;
    }

    private void EnsureGenerating()
    {
        if (Status != BriefStatus.Generating)
        {
            throw new InvalidOperationException(
                $"A brief in status {Status} is an immutable snapshot and cannot be modified.");
        }
    }

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
            throw new ArgumentException("Brief timestamps must be UTC.", parameterName);
        }
    }
}
