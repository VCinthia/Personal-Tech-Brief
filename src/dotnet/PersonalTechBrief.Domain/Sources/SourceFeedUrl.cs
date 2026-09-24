using System.Net;
using System.Net.Sockets;

namespace PersonalTechBrief.Domain.Sources;

/// <summary>
/// Validates and canonicalizes a feed endpoint before the application makes an outbound request.
/// DNS resolution remains outside this deterministic value boundary; the outbound HTTP boundary
/// resolves host names, rejects unsafe addresses, and pins its connection to a validated address.
/// </summary>
public static class SourceFeedUrl
{
    /// <summary>
    /// A practical public-API bound that permits normal feed endpoints while staying within SQL
    /// Server's 1,700-byte nonclustered-index key limit for the active normalized-URL constraint.
    /// </summary>
    public const int MaxLength = 850;

    public static string Clean(string value) => Prepare(value).FeedUrl;

    public static string Normalize(string value) => Prepare(value).NormalizedFeedUrl;

    public static Uri Parse(string value) => Prepare(value).Uri;

    private static PreparedSourceFeedUrl Prepare(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var cleaned = value.Trim();
        if (cleaned.Length == 0)
        {
            throw new ArgumentException("A feed URL cannot be empty.", nameof(value));
        }

        if (cleaned.Length > MaxLength)
        {
            throw new ArgumentException($"A feed URL cannot exceed {MaxLength} characters.", nameof(value));
        }

        if (!Uri.TryCreate(cleaned, UriKind.Absolute, out var uri) || !uri.IsWellFormedOriginalString())
        {
            throw new ArgumentException("A feed URL must be a well-formed absolute HTTP or HTTPS URL.", nameof(value));
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A feed URL must use HTTP or HTTPS.", nameof(value));
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("A feed URL must not contain credentials.", nameof(value));
        }

        if (IsLocalhostName(uri.DnsSafeHost) ||
            IsBlockedLiteralAddress(uri.Host))
        {
            throw new ArgumentException("A feed URL must not target a loopback or private address.", nameof(value));
        }

        // Do not case-fold paths or queries. They can be case-sensitive at the remote origin and
        // must remain distinct in the SQL Server uniqueness key. UriBuilder also removes a default
        // port when set to -1 and escapes Unicode before the persistence bound is checked.
        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.DnsSafeHost.ToLowerInvariant(),
            Port = uri.IsDefaultPort ? -1 : uri.Port,
            Fragment = string.Empty,
        };
        var canonicalUri = builder.Uri;
        var feedUrl = canonicalUri.AbsoluteUri;
        var normalizedFeedUrl = feedUrl;

        EnsureStoredLength(feedUrl);
        EnsureStoredLength(normalizedFeedUrl);
        return new PreparedSourceFeedUrl(canonicalUri, feedUrl, normalizedFeedUrl);
    }

    private static bool IsLocalhostName(string host)
    {
        var withoutTrailingDot = host.TrimEnd('.');
        return string.Equals(withoutTrailingDot, "localhost", StringComparison.OrdinalIgnoreCase) ||
               withoutTrailingDot.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureStoredLength(string value)
    {
        if (value.Length > MaxLength)
        {
            throw new ArgumentException(
                $"A feed URL cannot exceed {MaxLength} characters after canonicalization.",
                nameof(value));
        }
    }

    private static bool IsBlockedLiteralAddress(string host)
    {
        if (!IPAddress.TryParse(host, out var address))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || IPAddress.Any.Equals(address) || IPAddress.IPv6Any.Equals(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var octets = address.GetAddressBytes();
            return octets[0] == 0 ||
                   octets[0] == 10 ||
                   octets[0] == 127 ||
                   octets[0] >= 224 ||
                   (octets[0] == 100 && octets[1] is >= 64 and <= 127) ||
                   (octets[0] == 168 && octets[1] == 63 && octets[2] == 129 && octets[3] == 16) || // Azure platform virtual IP.
                   (octets[0] == 169 && octets[1] == 254) ||
                   (octets[0] == 172 && octets[1] is >= 16 and <= 31) ||
                   (octets[0] == 192 && octets[1] is 0 or 88 or 168) ||
                   (octets[0] == 198 && octets[1] is 18 or 19 or 51) ||
                   (octets[0] == 203 && octets[1] == 0);
        }

        var bytes = address.GetAddressBytes();
        return (bytes[0] & 0xe0) != 0x20 ||
               (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8) ||
               address.IsIPv6LinkLocal ||
               address.IsIPv6SiteLocal ||
               address.IsIPv6Multicast ||
               (bytes[0] & 0xfe) == 0xfc;
    }

    private sealed record PreparedSourceFeedUrl(Uri Uri, string FeedUrl, string NormalizedFeedUrl);
}
