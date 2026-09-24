using System.Net.Http.Headers;
using System.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Sources;

namespace PersonalTechBrief.Infrastructure.Feeds;

/// <summary>
/// Outbound-validation boundary for a source before it becomes enabled. It deliberately performs
/// no item extraction, persistence, retry policy, or periodic ingestion work.
/// </summary>
public sealed class RssAtomFeedValidator(
    IFeedHostAddressResolver hostAddressResolver,
    IFeedValidationHttpClientFactory httpClientFactory,
    IOptions<FeedValidationOptions> options,
    ILogger<RssAtomFeedValidator> logger) : IFeedValidator
{
    private const string AtomNamespace = "http://www.w3.org/2005/Atom";

    public async Task<bool> IsSupportedAsync(Uri feedUrl, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        using var requestSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        try
        {
            if (FeedHostAddressPolicy.IsLocalhostName(feedUrl.DnsSafeHost))
            {
                LogInvalidFeed(feedUrl, "host is reserved for localhost");
                return false;
            }

            var addresses = await hostAddressResolver.ResolveAsync(feedUrl.DnsSafeHost, requestSource.Token);
            if (addresses.Count == 0 || addresses.Any(address => !FeedHostAddressPolicy.IsPublicInternetAddress(address)))
            {
                LogInvalidFeed(feedUrl, "host resolved to an unsafe address");
                return false;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, feedUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atom+xml"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rss+xml"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml", 0.8));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml", 0.8));

            using var client = httpClientFactory.Create(feedUrl, addresses[0]);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                requestSource.Token);
            if (!response.IsSuccessStatusCode)
            {
                LogRejectedResponse(feedUrl, (int)response.StatusCode);
                return false;
            }

            if (response.Content.Headers.ContentLength is long contentLength && contentLength > settings.MaximumResponseBytes)
            {
                LogInvalidFeed(feedUrl, "response exceeded the configured size limit");
                return false;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(requestSource.Token);
            await using var boundedStream = new BoundedReadStream(responseStream, settings.MaximumResponseBytes);
            using var reader = XmlReader.Create(boundedStream, new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = settings.MaximumResponseBytes,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
            });

            return await ContainsSupportedFeedStructureAsync(reader, requestSource.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            LogInvalidFeed(feedUrl, "request timed out");
            return false;
        }
        catch (HttpRequestException)
        {
            LogInvalidFeed(feedUrl, "HTTP request failed");
            return false;
        }
        catch (System.Net.Sockets.SocketException)
        {
            LogInvalidFeed(feedUrl, "host could not be resolved or connected");
            return false;
        }
        catch (XmlException)
        {
            LogInvalidFeed(feedUrl, "XML could not be parsed safely");
            return false;
        }
        catch (FeedResponseTooLargeException)
        {
            LogInvalidFeed(feedUrl, "response exceeded the configured size limit");
            return false;
        }
        catch (IOException)
        {
            LogInvalidFeed(feedUrl, "response could not be read");
            return false;
        }
    }

    private static async Task<bool> ContainsSupportedFeedStructureAsync(XmlReader reader, CancellationToken cancellationToken)
    {
        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (string.Equals(reader.LocalName, "rss", StringComparison.OrdinalIgnoreCase))
            {
                return await ContainsRssChannelAsync(reader, cancellationToken);
            }

            if (string.Equals(reader.LocalName, "feed", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(reader.NamespaceURI, AtomNamespace, StringComparison.Ordinal))
            {
                return await ContainsRequiredAtomMetadataAsync(reader, cancellationToken);
            }

            return false;
        }

        return false;
    }

    private static async Task<bool> ContainsRssChannelAsync(XmlReader reader, CancellationToken cancellationToken)
    {
        var rootDepth = reader.Depth;
        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.Element &&
                reader.Depth == rootDepth + 1 &&
                string.Equals(reader.LocalName, "channel", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> ContainsRequiredAtomMetadataAsync(XmlReader reader, CancellationToken cancellationToken)
    {
        var rootDepth = reader.Depth;
        var hasId = false;
        var hasTitle = false;
        var hasUpdated = false;
        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element || reader.Depth != rootDepth + 1)
            {
                continue;
            }

            hasId |= string.Equals(reader.LocalName, "id", StringComparison.OrdinalIgnoreCase);
            hasTitle |= string.Equals(reader.LocalName, "title", StringComparison.OrdinalIgnoreCase);
            hasUpdated |= string.Equals(reader.LocalName, "updated", StringComparison.OrdinalIgnoreCase);
            if (hasId && hasTitle && hasUpdated)
            {
                return true;
            }
        }

        return false;
    }

    private void LogRejectedResponse(Uri feedUrl, int statusCode) =>
        logger.LogInformation(
            "Feed source validation did not receive a successful response. Host {FeedHost}, StatusCode {StatusCode}",
            feedUrl.Host,
            statusCode);

    private void LogInvalidFeed(Uri feedUrl, string reason) =>
        logger.LogInformation(
            "Feed source validation did not accept the response. Host {FeedHost}, Reason {Reason}",
            feedUrl.Host,
            reason);

    private sealed class FeedResponseTooLargeException : Exception;

    private sealed class BoundedReadStream(Stream inner, long maximumBytes) : Stream
    {
        private long bytesRead;

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(Limit(buffer));
            Track(read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(Limit(buffer), cancellationToken);
            Track(read);
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private Span<byte> Limit(Span<byte> buffer)
        {
            var remainingPlusOne = checked(maximumBytes - bytesRead + 1);
            return buffer[..(int)Math.Min(buffer.Length, remainingPlusOne)];
        }

        private Memory<byte> Limit(Memory<byte> buffer)
        {
            var remainingPlusOne = checked(maximumBytes - bytesRead + 1);
            return buffer[..(int)Math.Min(buffer.Length, remainingPlusOne)];
        }

        private void Track(int read)
        {
            bytesRead += read;
            if (bytesRead > maximumBytes)
            {
                throw new FeedResponseTooLargeException();
            }
        }
    }
}
