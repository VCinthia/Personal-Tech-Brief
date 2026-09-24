using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Infrastructure.Intelligence.Contracts;

namespace PersonalTechBrief.Infrastructure.Intelligence;

/// <summary>
/// <see cref="IIntelligenceApiClient"/> over an <see cref="HttpClient"/> supplied by
/// <see cref="System.Net.Http.IHttpClientFactory"/>. The client's own timeout is disabled; each
/// call enforces the configured per-endpoint timeout through a linked cancellation source so the
/// caller's token and the timeout are honored independently. Transient transport failures are
/// retried with exponential backoff and jitter; every received HTTP response (including provider
/// error codes the service has already exhausted its own retries on) is mapped directly.
/// </summary>
public sealed class HttpIntelligenceApiClient(
    HttpClient httpClient,
    IOptions<IntelligenceApiOptions> options,
    ILogger<HttpIntelligenceApiClient> logger) : IIntelligenceApiClient
{
    private const string AnalyzePath = "internal/v1/items/analyze";
    private const string SimilarityPath = "internal/v1/similarity";
    private const string GeneratePath = "internal/v1/updates/generate";

    private readonly IntelligenceApiOptions _options = options.Value;

    public Task<AnalyzeResponse> AnalyzeAsync(AnalyzeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync<AnalyzeRequest, AnalyzeResponse>(
            AnalyzePath,
            request,
            TimeSpan.FromSeconds(_options.AnalyzeTimeoutSeconds),
            cancellationToken);
    }

    public Task<SimilarityResponse> ComputeSimilarityAsync(SimilarityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync<SimilarityRequest, SimilarityResponse>(
            SimilarityPath,
            request,
            TimeSpan.FromSeconds(_options.SimilarityTimeoutSeconds),
            cancellationToken);
    }

    public Task<GenerateResponse> GenerateAsync(GenerateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync<GenerateRequest, GenerateResponse>(
            GeneratePath,
            request,
            TimeSpan.FromSeconds(_options.GenerateTimeoutSeconds),
            cancellationToken);
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        string path,
        TRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            attempt++;
            using var timeoutSource = new CancellationTokenSource(timeout);
            using var requestSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token);

            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path)
                {
                    Content = JsonContent.Create(request, options: IntelligenceApiJson.Options),
                };

                using var response = await httpClient.SendAsync(httpRequest, requestSource.Token);
                if (!response.IsSuccessStatusCode)
                {
                    throw await CreateApiExceptionAsync(path, response, requestSource.Token);
                }

                var payload = await response.Content.ReadFromJsonAsync<TResponse>(
                    IntelligenceApiJson.Options,
                    requestSource.Token);
                return payload ?? throw new IntelligenceApiException(
                    "The Intelligence API returned an empty response body.",
                    response.StatusCode);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException exception)
            {
                logger.LogWarning("Intelligence API request to {Path} timed out after {TimeoutSeconds}s.", path, timeout.TotalSeconds);
                throw new IntelligenceApiException(
                    "The Intelligence API request timed out.",
                    innerException: exception);
            }
            catch (HttpRequestException) when (attempt <= _options.MaxRetries)
            {
                logger.LogWarning("Transient transport failure calling Intelligence API {Path}; retrying (attempt {Attempt}).", path, attempt);
                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                throw new IntelligenceApiException(
                    "The Intelligence API request failed before a response was received.",
                    innerException: exception);
            }
        }
    }

    private async Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var baseDelay = _options.RetryBaseDelayMilliseconds;
        if (baseDelay <= 0)
        {
            return;
        }

        var exponential = baseDelay * Math.Pow(2, attempt - 1);
        var jitter = Random.Shared.Next(0, baseDelay + 1);
        var delay = TimeSpan.FromMilliseconds(exponential + jitter);
        await Task.Delay(delay, cancellationToken);
    }

    private async Task<IntelligenceApiException> CreateApiExceptionAsync(
        string path,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string? problemTitle = null;
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(body))
            {
                var problem = JsonSerializer.Deserialize<ProblemDetailsBody>(body, IntelligenceApiJson.Options);
                problemTitle = problem?.Title;
            }
        }
        catch (JsonException)
        {
            // The body was not a well-formed problem document. Do not surface it; status alone is safe.
        }

        logger.LogWarning(
            "Intelligence API {Path} returned status {StatusCode}.",
            path,
            (int)response.StatusCode);

        return new IntelligenceApiException(
            $"The Intelligence API responded with status {(int)response.StatusCode}.",
            response.StatusCode,
            problemTitle);
    }

    /// <summary>Minimal RFC 9457 problem document projection. Only the stable, non-sensitive fields are read.</summary>
    private sealed record ProblemDetailsBody
    {
        public string? Type { get; init; }

        public string? Title { get; init; }

        public int? Status { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extensions { get; init; }
    }
}
