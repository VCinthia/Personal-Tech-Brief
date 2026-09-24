namespace PersonalTechBrief.Domain.Sources;

using PersonalTechBrief.Domain.Ingestion;

public sealed class Source
{
    private Source()
    {
    }

    private Source(Guid id, string name, string feedUrl, DateTime utcNow)
    {
        Id = id;
        Apply(name, feedUrl, isEnabled: true, utcNow);
        CreatedAtUtc = utcNow;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string FeedUrl { get; private set; } = null!;

    public string NormalizedFeedUrl { get; private set; } = null!;

    public bool IsEnabled { get; private set; }

    public string? ETag { get; private set; }

    public DateTimeOffset? LastModified { get; private set; }

    public DateTime? LastIngestionAtUtc { get; private set; }

    public string? LastIngestionStatus { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public static Source Create(string name, string feedUrl, DateTime utcNow) =>
        new(Guid.NewGuid(), name, feedUrl, utcNow);

    public void Update(string name, string feedUrl, bool isEnabled, DateTime utcNow) =>
        Apply(name, feedUrl, isEnabled, utcNow);

    public void Enable(DateTime utcNow) => Apply(Name, FeedUrl, isEnabled: true, utcNow);

    public void Disable(DateTime utcNow) => Apply(Name, FeedUrl, isEnabled: false, utcNow);

    public void RecordIngestionOutcome(
        IngestionRunStatus status,
        DateTime completedAtUtc,
        string? eTag,
        DateTimeOffset? lastModified)
    {
        if (status == IngestionRunStatus.Running)
        {
            throw new ArgumentOutOfRangeException(nameof(status), "A source cannot record a running outcome.");
        }

        if (completedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Source ingestion timestamps must be UTC.", nameof(completedAtUtc));
        }

        if (eTag?.Length > 512)
        {
            throw new ArgumentException("A source ETag must be 512 characters or fewer.", nameof(eTag));
        }

        LastIngestionAtUtc = completedAtUtc;
        LastIngestionStatus = status.ToString();
        ETag = string.IsNullOrWhiteSpace(eTag) ? null : eTag.Trim();
        LastModified = lastModified;
        UpdatedAtUtc = completedAtUtc;
    }

    private void Apply(string name, string feedUrl, bool isEnabled, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Source timestamps must be UTC.", nameof(utcNow));
        }

        Name = SourceName.Clean(name);
        FeedUrl = SourceFeedUrl.Clean(feedUrl);
        NormalizedFeedUrl = SourceFeedUrl.Normalize(feedUrl);
        IsEnabled = isEnabled;
        UpdatedAtUtc = utcNow;
    }
}
