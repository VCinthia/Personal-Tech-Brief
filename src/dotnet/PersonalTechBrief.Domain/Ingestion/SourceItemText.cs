using System.Text;

namespace PersonalTechBrief.Domain.Ingestion;

public static class SourceItemText
{
    // This is indexed with RetrievedAtUtc for serializable title-window deduplication,
    // so it is kept below SQL Server's nonclustered-index key limit.
    public const int TitleMaxLength = 800;
    public const int ExcerptMaxLength = 4_000;
    public const int ExternalIdMaxLength = 512;
    public const int ContentHashMaxLength = 128;

    public static string CleanTitle(string value)
    {
        var cleaned = CollapseWhitespace(value);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            throw new ArgumentException("A source item requires a title.", nameof(value));
        }

        if (cleaned.Length > TitleMaxLength)
        {
            throw new ArgumentException($"A source item title must be {TitleMaxLength} characters or fewer.", nameof(value));
        }

        return cleaned;
    }

    public static string NormalizeTitle(string value) => CleanTitle(value).ToUpperInvariant();

    public static string? CleanExternalId(string? value) => CleanOptional(value, ExternalIdMaxLength, nameof(value));

    public static string? CleanExcerpt(string? value) => CleanOptional(value, ExcerptMaxLength, nameof(value));

    public static string? CleanContentHash(string? value) =>
        CleanOptional(value, ContentHashMaxLength, nameof(value))?.ToUpperInvariant();

    private static string? CleanOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim();
        if (cleaned.Length > maxLength)
        {
            throw new ArgumentException($"The value must be {maxLength} characters or fewer.", parameterName);
        }

        return cleaned;
    }

    private static string CollapseWhitespace(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;

        foreach (var character in value.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
