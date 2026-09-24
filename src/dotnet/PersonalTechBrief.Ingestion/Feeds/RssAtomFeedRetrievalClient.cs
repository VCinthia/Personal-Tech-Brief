using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Domain.Sources;
using PersonalTechBrief.Infrastructure.Feeds;

namespace PersonalTechBrief.Ingestion.Feeds;

/// <summary>
/// Performs one bounded, conditionally-requested RSS/Atom retrieval. DNS answers are inspected
/// once per retrieval and the selected public address is pinned for every retry by the HTTP
/// client factory, preventing redirects, proxy use, and DNS rebinding from changing the connection.
/// </summary>
public sealed class RssAtomFeedRetrievalClient(
    IFeedHostAddressResolver hostAddressResolver,
    IFeedValidationHttpClientFactory httpClientFactory,
    IOptions<FeedRetrievalOptions> options) : IFeedRetrievalClient
{
    public async Task<FeedRetrievalResult> RetrieveAsync(Source source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var feedUrl = SourceFeedUrl.Parse(source.FeedUrl);
        var settings = options.Value;
        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        using var requestSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        if (FeedHostAddressPolicy.IsLocalhostName(feedUrl.DnsSafeHost))
        {
            throw UnsafeHost();
        }

        IReadOnlyList<IPAddress> addresses;
        try
        {
            addresses = await hostAddressResolver.ResolveAsync(feedUrl.DnsSafeHost, requestSource.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw TimedOut(exception);
        }
        catch (Exception exception) when (exception is HttpRequestException or System.Net.Sockets.SocketException)
        {
            throw new FeedRetrievalException(
                "host_resolution_failed",
                "The feed host could not be resolved.",
                isTransient: true,
                innerException: exception);
        }

        if (addresses.Count == 0 || addresses.Any(address => !FeedHostAddressPolicy.IsPublicInternetAddress(address)))
        {
            throw UnsafeHost();
        }

        FeedRetrievalException? lastTransientFailure = null;
        for (var attempt = 1; attempt <= settings.MaximumAttempts; attempt++)
        {
            try
            {
                return await RetrieveOnceAsync(source, feedUrl, addresses[0], settings, requestSource.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException exception)
            {
                throw TimedOut(exception);
            }
            catch (HttpRequestException exception)
            {
                lastTransientFailure = new FeedRetrievalException(
                    "network_failure",
                    "The feed request failed before a response was received.",
                    isTransient: true,
                    innerException: exception);
            }
            catch (FeedRetrievalException exception) when (exception.IsTransient)
            {
                lastTransientFailure = exception;
            }

            if (lastTransientFailure is null || attempt == settings.MaximumAttempts)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(settings.RetryDelayMilliseconds), requestSource.Token);
        }

        throw lastTransientFailure ?? new FeedRetrievalException(
            "retrieval_failed",
            "The feed could not be retrieved.");
    }

    private async Task<FeedRetrievalResult> RetrieveOnceAsync(
        Source source,
        Uri feedUrl,
        IPAddress validatedAddress,
        FeedRetrievalOptions settings,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, feedUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atom+xml"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rss+xml"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml", 0.8));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml", 0.8));
        ApplyConditionals(request, source);

        using var client = httpClientFactory.Create(feedUrl, validatedAddress);
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var eTag = BoundEtag(response.Headers.ETag?.ToString());
        var lastModified = response.Content.Headers.LastModified;

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return FeedRetrievalResult.NotModified(eTag ?? source.ETag, lastModified ?? source.LastModified);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw ResponseFailure(response.StatusCode);
        }

        if (response.Content.Headers.ContentLength is long contentLength && contentLength > settings.MaximumResponseBytes)
        {
            throw new FeedRetrievalException(
                "response_too_large",
                "The feed response exceeded the configured size limit.");
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var items = await RssAtomFeedParser.ParseAsync(
            responseStream,
            feedUrl,
            settings.MaximumResponseBytes,
            cancellationToken);
        return new FeedRetrievalResult(false, items, eTag, lastModified);
    }

    private static void ApplyConditionals(HttpRequestMessage request, Source source)
    {
        if (!string.IsNullOrWhiteSpace(source.ETag) &&
            EntityTagHeaderValue.TryParse(source.ETag, out var eTag))
        {
            request.Headers.IfNoneMatch.Add(eTag);
        }

        if (source.LastModified.HasValue)
        {
            request.Headers.IfModifiedSince = source.LastModified;
        }
    }

    private static FeedRetrievalException ResponseFailure(HttpStatusCode statusCode)
    {
        var isTransient = statusCode == HttpStatusCode.RequestTimeout ||
                          statusCode == (HttpStatusCode)429 ||
                          (int)statusCode >= 500;
        return new FeedRetrievalException(
            isTransient ? "transient_http_status" : "rejected_http_status",
            $"The feed returned HTTP status {(int)statusCode}.",
            isTransient);
    }

    private static FeedRetrievalException UnsafeHost() =>
        new("unsafe_feed_host", "The feed host is not a public Internet address.");

    private static FeedRetrievalException TimedOut(Exception exception) =>
        new("request_timed_out", "The feed request exceeded its configured timeout.", true, exception);

    private static string? BoundEtag(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= 512 ? value : value[..512];
}
