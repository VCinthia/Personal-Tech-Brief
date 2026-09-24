using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace PersonalTechBrief.Infrastructure.Feeds;

/// <summary>
/// Creates a short-lived validation client whose connection is pinned to an address that was
/// resolved and approved for the source host immediately before the request.
/// </summary>
public interface IFeedValidationHttpClientFactory
{
    HttpClient Create(Uri feedUrl, IPAddress validatedAddress);
}

public sealed class PinnedAddressFeedValidationHttpClientFactory(
    IOptions<FeedValidationOptions> options) : IFeedValidationHttpClientFactory
{
    public HttpClient Create(Uri feedUrl, IPAddress validatedAddress)
    {
        var client = new HttpClient(CreateHandler(feedUrl, validatedAddress), disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(options.Value.UserAgent);
        return client;
    }

    /// <summary>
    /// Exposed for deterministic handler-policy tests. The request URI remains the original host,
    /// preserving HTTP Host and HTTPS SNI while ConnectCallback opens TCP to the pinned address.
    /// </summary>
    public SocketsHttpHandler CreateHandler(Uri feedUrl, IPAddress validatedAddress)
    {
        ArgumentNullException.ThrowIfNull(feedUrl);
        ArgumentNullException.ThrowIfNull(validatedAddress);

        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            SslOptions = new SslClientAuthenticationOptions { TargetHost = feedUrl.DnsSafeHost },
            ConnectCallback = (context, cancellationToken) => ConnectAsync(
                validatedAddress,
                context.DnsEndPoint.Port,
                cancellationToken),
        };
    }

    private static async ValueTask<Stream> ConnectAsync(
        IPAddress validatedAddress,
        int port,
        CancellationToken cancellationToken)
    {
        var socket = new Socket(validatedAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };

        try
        {
            await socket.ConnectAsync(new IPEndPoint(validatedAddress, port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
