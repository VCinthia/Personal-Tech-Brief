using System.Net;
using PersonalTechBrief.Infrastructure.Feeds;

namespace PersonalTechBrief.UnitTests.Sources;

public class FeedHostAddressPolicyTests
{
    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.0.1")]
    [InlineData("169.254.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("168.63.129.16")]
    [InlineData("100.64.0.1")]
    [InlineData("192.0.0.1")]
    [InlineData("192.0.2.1")]
    [InlineData("192.88.99.1")]
    [InlineData("198.18.0.1")]
    [InlineData("198.19.255.1")]
    [InlineData("198.51.100.1")]
    [InlineData("203.0.113.1")]
    [InlineData("224.0.0.1")]
    [InlineData("240.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("ff00::1")]
    [InlineData("2001:db8::1")]
    [InlineData("::ffff:10.0.0.1")]
    public void Rejects_non_public_or_special_use_addresses(string address)
    {
        Assert.False(FeedHostAddressPolicy.IsPublicInternetAddress(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    // Public unicast that shares the first octet(s) with a reserved block but is outside its exact
    // IANA CIDR must not be over-blocked (192.0.66.2 hosts github.blog on WordPress VIP).
    [InlineData("192.0.66.2")]
    [InlineData("192.0.1.1")]
    [InlineData("192.88.1.1")]
    [InlineData("198.51.1.1")]
    [InlineData("203.0.1.1")]
    [InlineData("2606:4700:4700::1111")]
    public void Allows_public_global_unicast_addresses(string address)
    {
        Assert.True(FeedHostAddressPolicy.IsPublicInternetAddress(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("localhost.")]
    [InlineData("feed.localhost")]
    [InlineData("feed.localhost.")]
    public void Rejects_localhost_names_including_trailing_dot_forms(string host)
    {
        Assert.True(FeedHostAddressPolicy.IsLocalhostName(host));
    }
}
