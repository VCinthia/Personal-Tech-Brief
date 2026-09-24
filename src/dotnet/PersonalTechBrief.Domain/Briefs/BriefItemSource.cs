namespace PersonalTechBrief.Domain.Briefs;

/// <summary>
/// The value used to build a <see cref="BriefItemSource"/> before the owning <see cref="BriefItem"/>
/// has an identity. The item creates its source rows from these snapshots so each source is bound to
/// the item's generated id.
/// </summary>
public sealed record BriefItemSourceSnapshot(
    Guid SourceItemId,
    string TitleSnapshot,
    string? UrlSnapshot,
    DateTime? PublishedAtUtc);

/// <summary>
/// An immutable snapshot of one supporting source reference for a <see cref="BriefItem"/>
/// (FR-012/FR-017/AC-010). The title and URL are captured at generation time so the brief keeps a
/// stable, openable attribution even if the live source is later disabled or removed.
/// </summary>
public sealed class BriefItemSource
{
    public const int TitleSnapshotMaxLength = 512;
    public const int UrlSnapshotMaxLength = 2048;

    private BriefItemSource()
    {
    }

    private BriefItemSource(
        Guid id,
        Guid briefItemId,
        Guid sourceItemId,
        string titleSnapshot,
        string? urlSnapshot,
        DateTime? publishedAtUtc)
    {
        if (briefItemId == Guid.Empty)
        {
            throw new ArgumentException("A brief item source requires a brief item identifier.", nameof(briefItemId));
        }

        if (sourceItemId == Guid.Empty)
        {
            throw new ArgumentException("A brief item source requires a source item identifier.", nameof(sourceItemId));
        }

        if (publishedAtUtc is { } published && published.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("A brief item source published timestamp must be UTC.", nameof(publishedAtUtc));
        }

        Id = id;
        BriefItemId = briefItemId;
        SourceItemId = sourceItemId;
        TitleSnapshot = CleanRequiredText(titleSnapshot, TitleSnapshotMaxLength, nameof(titleSnapshot));
        UrlSnapshot = CleanOptionalText(urlSnapshot, UrlSnapshotMaxLength);
        PublishedAtUtc = publishedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid BriefItemId { get; private set; }

    public Guid SourceItemId { get; private set; }

    public string TitleSnapshot { get; private set; } = null!;

    /// <summary>The original/canonical source URL captured at generation time; null when unknown.</summary>
    public string? UrlSnapshot { get; private set; }

    public DateTime? PublishedAtUtc { get; private set; }

    public static BriefItemSource Create(
        Guid briefItemId,
        Guid sourceItemId,
        string titleSnapshot,
        string? urlSnapshot,
        DateTime? publishedAtUtc) =>
        new(Guid.NewGuid(), briefItemId, sourceItemId, titleSnapshot, urlSnapshot, publishedAtUtc);

    private static string CleanRequiredText(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value is required.", parameterName);
        }

        var cleaned = value.Trim();
        return cleaned.Length > maxLength ? cleaned[..maxLength] : cleaned;
    }

    private static string? CleanOptionalText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        return cleaned.Length > maxLength ? cleaned[..maxLength] : cleaned;
    }
}
