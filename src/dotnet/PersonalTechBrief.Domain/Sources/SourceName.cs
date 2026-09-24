namespace PersonalTechBrief.Domain.Sources;

/// <summary>
/// Applies the bounded display-name rules for a configured RSS/Atom source.
/// </summary>
public static class SourceName
{
    /// <summary>
    /// Keeps source names within SQL Server's indexed-key limit and the public API's documented bound.
    /// </summary>
    public const int MaxLength = 200;

    public static string Clean(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var cleaned = value.Trim();
        if (cleaned.Length == 0)
        {
            throw new ArgumentException("A source name cannot be empty.", nameof(value));
        }

        if (cleaned.Length > MaxLength)
        {
            throw new ArgumentException($"A source name cannot exceed {MaxLength} characters.", nameof(value));
        }

        return cleaned;
    }
}
