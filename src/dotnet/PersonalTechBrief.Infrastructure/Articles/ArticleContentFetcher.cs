using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Infrastructure.Feeds;

namespace PersonalTechBrief.Infrastructure.Articles;

public sealed record ArticleFetchResult(bool Succeeded, string? Html, string? FinalUrl, string? FailureReason)
{
    public static ArticleFetchResult Success(string html, string finalUrl) => new(true, html, finalUrl, null);

    public static ArticleFetchResult Failed(string reason) => new(false, null, null, reason);
}

/// <summary>
/// SSRF-safe one-off fetch for a user-submitted article URL (FR-003, security spec §15).
/// </summary>
public interface IArticleContentFetcher
{
    Task<ArticleFetchResult> FetchAsync(Uri url, CancellationToken cancellationToken);
}

/// <summary>
/// A fully-materialized single-hop response. The body is read (bounded) and the transport is
/// disposed before this is returned, so the caller holds no live connection. A redirect carries a
/// <see cref="Location"/> and no body; a body over the cap sets <see cref="ExceededCap"/>.
/// </summary>
public sealed record PinnedArticleResponse(
    int StatusCode,
    Uri? Location,
    string? MediaType,
    string? Body,
    bool ExceededCap);

/// <summary>
/// Sends a single GET pinned to a pre-validated address and returns a materialized response. The
/// seam keeps the SSRF host policy and redirect decisions in <see cref="ArticleContentFetcher"/>
/// while making the transport substitutable in tests. Redirects are never followed here.
/// </summary>
public interface IPinnedArticleRequester
{
    Task<PinnedArticleResponse> SendAsync(
        Uri url,
        IPAddress pinnedAddress,
        string userAgent,
        long maximumBytes,
        CancellationToken cancellationToken);
}

public sealed class PinnedArticleRequester : IPinnedArticleRequester
{
    private static readonly HashSet<int> RedirectStatusCodes = [301, 302, 303, 307, 308];

    public async Task<PinnedArticleResponse> SendAsync(
        Uri url,
        IPAddress pinnedAddress,
        string userAgent,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var handler = CreateHandler(url, pinnedAddress);
        using var client = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml", 0.9));
        request.Headers.UserAgent.ParseAdd(userAgent);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (RedirectStatusCodes.Contains((int)response.StatusCode))
        {
            return new PinnedArticleResponse((int)response.StatusCode, response.Headers.Location, null, null, false);
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (response.Content.Headers.ContentLength is long declared && declared > maximumBytes)
        {
            return new PinnedArticleResponse((int)response.StatusCode, null, mediaType, null, true);
        }

        var (body, exceeded) = await ReadBoundedAsync(response, maximumBytes, cancellationToken);
        return new PinnedArticleResponse((int)response.StatusCode, null, mediaType, exceeded ? null : body, exceeded);
    }

    /// <summary>
    /// Builds a handler pinned to the pre-validated address. Auto-redirects are disabled so each
    /// hop is re-validated by the fetcher; the request URI keeps the original host for Host/SNI.
    /// </summary>
    public static SocketsHttpHandler CreateHandler(Uri url, IPAddress validatedAddress)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(validatedAddress);

        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.All,
            SslOptions = new SslClientAuthenticationOptions { TargetHost = url.DnsSafeHost },
            ConnectCallback = async (context, token) =>
            {
                var socket = new Socket(validatedAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(validatedAddress, context.DnsEndPoint.Port), token);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
    }

    private static async Task<(string? Body, bool Exceeded)> ReadBoundedAsync(
        HttpResponseMessage response,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maximumBytes)
            {
                return (null, true);
            }

            buffer.Write(chunk, 0, read);
        }

        var encoding = ResolveEncoding(response.Content.Headers.ContentType?.CharSet);
        return (encoding.GetString(buffer.GetBuffer(), 0, (int)buffer.Length), false);
    }

    private static Encoding ResolveEncoding(string? charSet)
    {
        if (string.IsNullOrWhiteSpace(charSet))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charSet.Trim().Trim('"'));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }
}

/// <summary>
/// Follows redirects manually with a bounded hop count and re-validates every hop's host against
/// the public-address policy before connecting. Each connection is pinned to the pre-resolved,
/// approved IP address, so a mixed or rebinding DNS answer cannot reach a private target. Only
/// <c>text/html</c>/<c>application/xhtml+xml</c> responses within the size cap are accepted; raw
/// content is never logged.
/// </summary>
public sealed class ArticleContentFetcher(
    IFeedHostAddressResolver hostAddressResolver,
    IPinnedArticleRequester requester,
    IOptions<ManualArticleFetchOptions> options,
    ILogger<ArticleContentFetcher> logger) : IArticleContentFetcher
{
    private static readonly HashSet<int> RedirectStatusCodes = [301, 302, 303, 307, 308];

    public async Task<ArticleFetchResult> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        var settings = options.Value;

        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        using var requestSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        var current = url;
        try
        {
            for (var hop = 0; hop <= settings.MaxRedirects; hop++)
            {
                if (current.Scheme != Uri.UriSchemeHttp && current.Scheme != Uri.UriSchemeHttps)
                {
                    return ArticleFetchResult.Failed("redirect left the http/https scheme");
                }

                if (FeedHostAddressPolicy.IsLocalhostName(current.DnsSafeHost))
                {
                    return ArticleFetchResult.Failed("host is reserved for localhost");
                }

                var addresses = await hostAddressResolver.ResolveAsync(current.DnsSafeHost, requestSource.Token);
                if (addresses.Count == 0 || addresses.Any(address => !FeedHostAddressPolicy.IsPublicInternetAddress(address)))
                {
                    return ArticleFetchResult.Failed("host resolved to an unsafe address");
                }

                var response = await requester.SendAsync(current, addresses[0], settings.UserAgent, settings.MaximumResponseBytes, requestSource.Token);

                if (RedirectStatusCodes.Contains(response.StatusCode))
                {
                    if (response.Location is null)
                    {
                        return ArticleFetchResult.Failed("redirect without a location");
                    }

                    current = response.Location.IsAbsoluteUri ? response.Location : new Uri(current, response.Location);
                    continue;
                }

                if (response.StatusCode is < 200 or >= 300)
                {
                    logger.LogInformation("Manual article fetch rejected with status {StatusCode}.", response.StatusCode);
                    return ArticleFetchResult.Failed("the article could not be retrieved");
                }

                if (response.MediaType is not ("text/html" or "application/xhtml+xml"))
                {
                    return ArticleFetchResult.Failed("the response was not an HTML document");
                }

                if (response.ExceededCap || response.Body is null)
                {
                    return ArticleFetchResult.Failed("the response exceeded the size limit");
                }

                return ArticleFetchResult.Success(response.Body, current.ToString());
            }

            return ArticleFetchResult.Failed("too many redirects");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return ArticleFetchResult.Failed("the request timed out");
        }
        catch (HttpRequestException)
        {
            return ArticleFetchResult.Failed("the article host could not be reached");
        }
    }
}
