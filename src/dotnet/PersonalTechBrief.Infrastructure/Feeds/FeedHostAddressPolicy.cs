using System.Net;
using System.Net.Sockets;

namespace PersonalTechBrief.Infrastructure.Feeds;

/// <summary>
/// Allows only public, globally-routable unicast addresses for source validation. A single unsafe
/// DNS answer rejects the host so a mixed public/private answer cannot bypass the policy.
/// </summary>
public static class FeedHostAddressPolicy
{
    public static bool IsLocalhostName(string host)
    {
        var withoutTrailingDot = host.TrimEnd('.');
        return string.Equals(withoutTrailingDot, "localhost", StringComparison.OrdinalIgnoreCase) ||
               withoutTrailingDot.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPublicInternetAddress(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicIpv4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsPublicIpv6(address),
            _ => false,
        };
    }

    private static bool IsPublicIpv4(byte[] octets)
    {
        if (IPAddress.IsLoopback(new IPAddress(octets)) || octets[0] is 0 or >= 224)
        {
            return false;
        }

        return !(octets[0] == 10 ||
                 (octets[0] == 100 && octets[1] is >= 64 and <= 127) || // Shared CGNAT.
                 (octets[0] == 127) ||
                 (octets[0] == 168 && octets[1] == 63 && octets[2] == 129 && octets[3] == 16) || // Azure platform virtual IP.
                 (octets[0] == 169 && octets[1] == 254) ||
                 (octets[0] == 172 && octets[1] is >= 16 and <= 31) ||
                 // Reserved special-use blocks are their exact IANA CIDRs, not the whole /16, so
                 // legitimate public unicast in the same first two octets (e.g. 192.0.66.0/24) is
                 // not over-blocked. These only ever narrow what is rejected; nothing internal is
                 // opened up.
                 (octets[0] == 192 && octets[1] == 0 && octets[2] == 0) ||   // 192.0.0.0/24 IETF protocol assignments.
                 (octets[0] == 192 && octets[1] == 0 && octets[2] == 2) ||   // 192.0.2.0/24 TEST-NET-1.
                 (octets[0] == 192 && octets[1] == 88 && octets[2] == 99) || // 192.88.99.0/24 6to4 relay anycast.
                 (octets[0] == 192 && octets[1] == 168) ||                   // 192.168.0.0/16 private.
                 (octets[0] == 198 && octets[1] is 18 or 19) ||              // 198.18.0.0/15 benchmarking.
                 (octets[0] == 198 && octets[1] == 51 && octets[2] == 100) || // 198.51.100.0/24 TEST-NET-2.
                 (octets[0] == 203 && octets[1] == 0 && octets[2] == 113));  // 203.0.113.0/24 TEST-NET-3.
    }

    private static bool IsPublicIpv6(IPAddress address)
    {
        var bytes = address.GetAddressBytes();

        // Global unicast is 2000::/3. This excludes unspecified, loopback, IPv4-mapped,
        // link-local, unique-local, multicast, and other special-use IPv6 ranges.
        return (bytes[0] & 0xe0) == 0x20 &&
               !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8);
    }
}
