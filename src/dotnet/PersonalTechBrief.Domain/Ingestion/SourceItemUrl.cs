namespace PersonalTechBrief.Domain.Ingestion;

public static class SourceItemUrl
{
    public const int MaxLength = 2_048;

    public static (string? CanonicalUrl, string? NormalizedUrl) Prepare(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, null);
        }

        var trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("A source item URL must be an absolute HTTP or HTTPS URL.", nameof(value));
        }

        var normalizedUri = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.Host.ToLowerInvariant(),
            Port = uri.IsDefaultPort ? -1 : uri.Port,
            Fragment = string.Empty,
        }.Uri.AbsoluteUri;

        if (normalizedUri.Length > MaxLength)
        {
            throw new ArgumentException($"A source item URL must be {MaxLength} characters or fewer after canonicalization.", nameof(value));
        }

        // CanonicalUrl keeps the source-provided path/query spelling while NormalizedUrl provides
        // a stable comparison value. The original URL is retained separately on SourceItem.
        return (normalizedUri, normalizedUri);
    }
}
