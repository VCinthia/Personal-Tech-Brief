namespace PersonalTechBrief.Domain.Interests;

public static class InterestName
{
    /// <summary>
    /// Maximum interest-name length. At 200 Unicode characters, the name and its normalized
    /// representation remain comfortably within SQL Server's indexed-key limit.
    /// </summary>
    public const int MaxLength = 200;

    public static string Clean(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var cleaned = value.Trim();

        if (cleaned.Length == 0)
        {
            throw new ArgumentException("An interest name cannot be empty.", nameof(value));
        }

        if (cleaned.Length > MaxLength)
        {
            throw new ArgumentException($"An interest name cannot exceed {MaxLength} characters.", nameof(value));
        }

        return cleaned;
    }

    public static string Normalize(string value) => Clean(value).ToUpperInvariant();
}
