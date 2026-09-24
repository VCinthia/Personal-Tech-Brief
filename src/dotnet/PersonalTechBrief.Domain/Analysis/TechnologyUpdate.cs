namespace PersonalTechBrief.Domain.Analysis;

/// <summary>
/// The user-visible underlying event/update that may be supported by several source items
/// (FR-007/008). Grouping links source items to one update; the current relevance score
/// (calibration §20) is recomputed as supporting items are added. Historical brief snapshots
/// are produced in later slices; this live entity is updated in place.
/// </summary>
public sealed class TechnologyUpdate
{
    public const int RepresentativeTitleMaxLength = 512;
    public const int PrimaryTopicMaxLength = 256;

    private TechnologyUpdate()
    {
    }

    private TechnologyUpdate(
        Guid id,
        string representativeTitle,
        string primaryTopic,
        DateTime observedAtUtc)
    {
        EnsureUtc(observedAtUtc, nameof(observedAtUtc));

        Id = id;
        RepresentativeTitle = CleanRequiredText(representativeTitle, RepresentativeTitleMaxLength, nameof(representativeTitle));
        PrimaryTopic = CleanRequiredText(primaryTopic, PrimaryTopicMaxLength, nameof(primaryTopic));
        Status = TechnologyUpdateStatus.Active;
        FirstObservedAtUtc = observedAtUtc;
        LastObservedAtUtc = observedAtUtc;
        CurrentRelevanceScore = 0d;
        CreatedAtUtc = observedAtUtc;
        UpdatedAtUtc = observedAtUtc;
    }

    public Guid Id { get; private set; }

    public string RepresentativeTitle { get; private set; } = null!;

    public string PrimaryTopic { get; private set; } = null!;

    public DateTime FirstObservedAtUtc { get; private set; }

    public DateTime LastObservedAtUtc { get; private set; }

    public TechnologyUpdateStatus Status { get; private set; }

    public double CurrentRelevanceScore { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public static TechnologyUpdate Create(string representativeTitle, string primaryTopic, DateTime observedAtUtc) =>
        new(Guid.NewGuid(), representativeTitle, primaryTopic, observedAtUtc);

    /// <summary>
    /// Widens the observed window to include another supporting source item. Idempotent for a
    /// timestamp already inside the window, so at-least-once redelivery never corrupts the range.
    /// </summary>
    public void RegisterSupportingObservation(DateTime observedAtUtc, DateTime utcNow)
    {
        EnsureUtc(observedAtUtc, nameof(observedAtUtc));
        EnsureUtc(utcNow, nameof(utcNow));

        if (observedAtUtc < FirstObservedAtUtc)
        {
            FirstObservedAtUtc = observedAtUtc;
        }

        if (observedAtUtc > LastObservedAtUtc)
        {
            LastObservedAtUtc = observedAtUtc;
        }

        UpdatedAtUtc = utcNow;
    }

    /// <summary>Stores the recomputed deterministic relevance score (calibration §20).</summary>
    public void SetRelevanceScore(double score, DateTime utcNow)
    {
        if (double.IsNaN(score) || double.IsInfinity(score))
        {
            throw new ArgumentOutOfRangeException(nameof(score), "A relevance score must be a finite number.");
        }

        EnsureUtc(utcNow, nameof(utcNow));

        CurrentRelevanceScore = score;
        UpdatedAtUtc = utcNow;
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
            throw new ArgumentException("Technology update timestamps must be UTC.", parameterName);
        }
    }
}
