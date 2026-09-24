using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using PersonalTechBrief.Application.Articles;
using PersonalTechBrief.Domain.Ingestion;
using PersonalTechBrief.Infrastructure.Feeds;

namespace PersonalTechBrief.Infrastructure.Articles;

/// <summary>
/// Orchestrates a one-off manual article submission (FR-003, UC-004): validate the URL, fetch it
/// under the SSRF-safe policy, extract a bounded title/text, and hand a source-less item to the
/// normal pipeline. It never registers the host as a permanent source (AC-011).
/// </summary>
public sealed class ManualArticleSubmissionService(
    IArticleContentFetcher fetcher,
    IArticleTextExtractor extractor,
    IManualArticleStore store,
    TimeProvider timeProvider,
    ILogger<ManualArticleSubmissionService> logger) : IManualArticleSubmissionService
{
    public async Task<ManualArticleSubmissionResult> SubmitAsync(string? url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) ||
            FeedHostAddressPolicy.IsLocalhostName(parsed.DnsSafeHost) ||
            IsReservedIpLiteral(parsed.DnsSafeHost))
        {
            return ManualArticleSubmissionResult.InvalidUrl();
        }

        var fetch = await fetcher.FetchAsync(parsed, cancellationToken);
        if (!fetch.Succeeded || fetch.Html is null)
        {
            logger.LogInformation("Manual article submission could not be fetched: {Reason}.", fetch.FailureReason);
            return ManualArticleSubmissionResult.Unfetchable();
        }

        var extraction = extractor.Extract(fetch.Html);
        if (extraction is null)
        {
            logger.LogInformation("Manual article submission produced no extractable title/text.");
            return ManualArticleSubmissionResult.NotExtractable();
        }

        var finalUrl = fetch.FinalUrl ?? parsed.ToString();
        var command = new PersistManualArticleCommand(
            finalUrl,
            Truncate(extraction.Title, SourceItemText.TitleMaxLength),
            Truncate(extraction.Text, SourceItemText.ExcerptMaxLength),
            ComputeContentHash(extraction.Text),
            Guid.NewGuid(),
            timeProvider.GetUtcNow().UtcDateTime,
            Activity.Current?.Id);

        var result = await store.PersistIfNewAsync(command, cancellationToken);
        return result.IsDuplicate
            ? ManualArticleSubmissionResult.Duplicate(result.SourceItemId)
            : ManualArticleSubmissionResult.Queued(result.SourceItemId!.Value);
    }

    // A URL whose host is a reserved (non-public) IP literal is rejected up front as an invalid
    // request (400), matching the frozen contract's "host is reserved" classification, rather than
    // being fetched and blocked later. A public IP literal or a DNS name is left to the fetch-time
    // resolve-and-validate policy.
    private static bool IsReservedIpLiteral(string host) =>
        System.Net.IPAddress.TryParse(host, out var address) &&
        !FeedHostAddressPolicy.IsPublicInternetAddress(address);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string ComputeContentHash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
