using System.Net;

namespace PersonalTechBrief.Infrastructure.Feeds;

/// <summary>
/// Resolves a feed host at the outbound-validation boundary. The abstraction keeps DNS-dependent
/// behavior deterministic in tests and ensures every resolved address can be inspected before use.
/// </summary>
public interface IFeedHostAddressResolver
{
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken);
}

public sealed class DnsFeedHostAddressResolver : IFeedHostAddressResolver
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
        await Dns.GetHostAddressesAsync(host).WaitAsync(cancellationToken);
}
