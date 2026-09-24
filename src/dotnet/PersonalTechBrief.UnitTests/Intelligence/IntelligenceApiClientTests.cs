using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Infrastructure.Intelligence;
using PersonalTechBrief.Infrastructure.Intelligence.Contracts;

namespace PersonalTechBrief.UnitTests.Intelligence;

public class IntelligenceApiClientTests
{
    private static readonly Uri BaseAddress = new("https://intelligence.internal.test/");

    private static readonly Guid CorrelationId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private static readonly Guid SourceItemId = Guid.Parse("9b2e6f3a-1c4d-4e5a-8b7c-0d1e2f3a4b5c");
    private static readonly Guid InterestA = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
    private static readonly Guid InterestB = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

    [Fact]
    public async Task AnalyzeAsync_serializes_request_as_camelcase_with_string_enums()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, AnalyzeResponseJson);
        using var context = CreateClient(handler);

        var request = new AnalyzeRequest
        {
            CorrelationId = CorrelationId,
            Item = new AnalyzeItem
            {
                SourceItemId = SourceItemId,
                Title = "New LTS runtime released",
                Excerpt = "A summary of the release.",
                Content = "Full article content about the release.",
                Language = "en",
            },
            CandidateInterests =
            [
                new CandidateInterest { InterestId = InterestA, Name = "Runtime platforms", Priority = InterestPriority.High },
                new CandidateInterest { InterestId = InterestB, Name = "Security advisories", Priority = InterestPriority.Medium },
            ],
        };

        await context.Client.AnalyzeAsync(request, CancellationToken.None);

        Assert.Equal("https://intelligence.internal.test/internal/v1/items/analyze", handler.RequestUris.Single()!.AbsoluteUri);
        var sent = JsonNode.Parse(handler.RequestBodies.Single()!)!;
        Assert.Equal("3fa85f64-5717-4562-b3fc-2c963f66afa6", sent["correlationId"]!.GetValue<string>());
        Assert.Equal("high", sent["candidateInterests"]![0]!["priority"]!.GetValue<string>());
        Assert.Equal("medium", sent["candidateInterests"]![1]!["priority"]!.GetValue<string>());
        Assert.Equal("New LTS runtime released", sent["item"]!["title"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(AnalyzeRequestJson), sent), "Analyze request JSON drifted from the frozen contract.");
    }

    [Fact]
    public async Task AnalyzeAsync_deserializes_response_including_enums_and_bounded_floats()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, AnalyzeResponseJson);
        using var context = CreateClient(handler);

        var response = await context.Client.AnalyzeAsync(MinimalAnalyzeRequest(), CancellationToken.None);

        Assert.Equal(CorrelationId, response.CorrelationId);
        Assert.Equal(SourceItemId, response.SourceItemId);
        Assert.Equal("analyze-1", response.AnalyzerVersion);
        Assert.Equal("en", response.Language);
        Assert.Equal(["runtime", "lts", "release"], response.Normalized.Keywords);
        Assert.Equal(["version-release"], response.Normalized.EventDescriptors);
        Assert.Equal(["runtime-platforms", "release-management"], response.Topics);
        Assert.Equal(ImpactLevel.High, response.Impact.Level);
        Assert.Equal(0.81, response.Impact.Confidence);

        // Order preserved and one entry per supplied interest id.
        Assert.Equal([InterestA, InterestB], response.InterestMatches.Select(match => match.InterestId));
        Assert.Equal(0.92, response.InterestMatches[0].MatchStrength);
        Assert.Equal(0.15, response.InterestMatches[1].MatchStrength);

        Assert.All(response.InterestMatches, match => Assert.InRange(match.MatchStrength, 0.0, 1.0));
        Assert.InRange(response.Impact.Confidence, 0.0, 1.0);
    }

    [Fact]
    public async Task AnalyzeAsync_accepts_null_language_and_impact_none()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, AnalyzeResponseNoImpactJson);
        using var context = CreateClient(handler);

        var response = await context.Client.AnalyzeAsync(MinimalAnalyzeRequest(), CancellationToken.None);

        Assert.Null(response.Language);
        Assert.Equal(ImpactLevel.None, response.Impact.Level);
        Assert.Equal(0.0, response.Impact.Confidence);
        Assert.Empty(response.InterestMatches);
    }

    [Fact]
    public async Task ComputeSimilarityAsync_round_trips_and_preserves_result_order()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, SimilarityResponseJson);
        using var context = CreateClient(handler);

        var request = new SimilarityRequest
        {
            CorrelationId = CorrelationId,
            Candidate = new SimilarityCandidate { Text = "candidate update text" },
            Representatives =
            [
                new SimilarityRepresentative { UpdateId = Rep1, Text = "first representative" },
                new SimilarityRepresentative { UpdateId = Rep2, Text = "second representative" },
                new SimilarityRepresentative { UpdateId = Rep3, Text = "third representative" },
            ],
        };

        var response = await context.Client.ComputeSimilarityAsync(request, CancellationToken.None);

        Assert.Equal("https://intelligence.internal.test/internal/v1/similarity", handler.RequestUris.Single()!.AbsoluteUri);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(SimilarityRequestJson), JsonNode.Parse(handler.RequestBodies.Single()!)), "Similarity request JSON drifted from the frozen contract.");

        Assert.Equal("similarity-1", response.AlgorithmVersion);
        Assert.Equal([Rep1, Rep2, Rep3], response.Results.Select(result => result.UpdateId));
        Assert.Equal(1.0, response.Results[0].Similarity);
        Assert.Equal(0.4213, response.Results[1].Similarity);
        Assert.Equal(0.0, response.Results[2].Similarity);
        Assert.All(response.Results, result => Assert.InRange(result.Similarity, 0.0, 1.0));
    }

    [Fact]
    public async Task ComputeSimilarityAsync_returns_empty_results_for_empty_representatives()
    {
        const string json = """
        {"correlationId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","algorithmVersion":"similarity-1","results":[]}
        """;
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, json);
        using var context = CreateClient(handler);

        var response = await context.Client.ComputeSimilarityAsync(
            new SimilarityRequest
            {
                CorrelationId = CorrelationId,
                Candidate = new SimilarityCandidate { Text = "candidate" },
                Representatives = [],
            },
            CancellationToken.None);

        Assert.Empty(response.Results);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "Malformed request body")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "Unprocessable content")]
    [InlineData(HttpStatusCode.BadGateway, "Semantic analysis provider failed")]
    [InlineData(HttpStatusCode.GatewayTimeout, "Semantic analysis provider timed out")]
    public async Task AnalyzeAsync_maps_problem_json_errors_to_typed_exception(HttpStatusCode statusCode, string title)
    {
        var problemJson = $$"""
        {"type":"https://intelligence.internal/errors/analyze","title":"{{title}}","status":{{(int)statusCode}}}
        """;
        var handler = StubHttpMessageHandler.Returning(statusCode, problemJson, "application/problem+json");
        using var context = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<IntelligenceApiException>(
            () => context.Client.AnalyzeAsync(MinimalAnalyzeRequest(), CancellationToken.None));

        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Equal(title, exception.ProblemTitle);
        Assert.Equal(1, handler.CallCount); // received HTTP responses are not retried
        Assert.DoesNotContain("provider", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_maps_non_problem_error_body_without_leaking_it()
    {
        const string secretLeak = "stack trace: connection string=Server=secret;Password=hunter2";
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.BadGateway, secretLeak, "text/plain");
        using var context = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<IntelligenceApiException>(
            () => context.Client.AnalyzeAsync(MinimalAnalyzeRequest(), CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.Null(exception.ProblemTitle);
        Assert.DoesNotContain("hunter2", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeAsync_maps_configured_timeout_to_typed_exception()
    {
        var handler = new StubHttpMessageHandler(async (_, _, _, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var context = CreateClient(handler, new IntelligenceApiOptions { AnalyzeTimeoutSeconds = 0 });

        var exception = await Assert.ThrowsAsync<IntelligenceApiException>(
            () => context.Client.AnalyzeAsync(MinimalAnalyzeRequest(), CancellationToken.None));

        Assert.Null(exception.StatusCode);
        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_propagates_caller_cancellation()
    {
        var handler = new StubHttpMessageHandler(async (_, _, _, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var context = CreateClient(handler);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Client.AnalyzeAsync(MinimalAnalyzeRequest(), cancellation.Token));
    }

    [Fact]
    public async Task AnalyzeAsync_retries_transient_transport_failures_then_succeeds()
    {
        var handler = new StubHttpMessageHandler((attempt, _, _, _) => attempt < 3
            ? throw new HttpRequestException("connection reset")
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AnalyzeResponseJson, System.Text.Encoding.UTF8, "application/json"),
            }));
        using var context = CreateClient(handler, new IntelligenceApiOptions { MaxRetries = 3, RetryBaseDelayMilliseconds = 1 });

        var response = await context.Client.AnalyzeAsync(MinimalAnalyzeRequest(), CancellationToken.None);

        Assert.Equal(3, handler.CallCount);
        Assert.Equal("analyze-1", response.AnalyzerVersion);
    }

    [Fact]
    public async Task AnalyzeAsync_throws_typed_exception_when_transport_retries_exhausted()
    {
        var handler = new StubHttpMessageHandler((_, _, _, _) => throw new HttpRequestException("connection reset"));
        using var context = CreateClient(handler, new IntelligenceApiOptions { MaxRetries = 2, RetryBaseDelayMilliseconds = 1 });

        var exception = await Assert.ThrowsAsync<IntelligenceApiException>(
            () => context.Client.AnalyzeAsync(MinimalAnalyzeRequest(), CancellationToken.None));

        Assert.Null(exception.StatusCode);
        Assert.Equal(3, handler.CallCount); // initial attempt + 2 retries
    }

    private static readonly Guid Rep1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Rep2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Rep3 = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static AnalyzeRequest MinimalAnalyzeRequest() => new()
    {
        CorrelationId = CorrelationId,
        Item = new AnalyzeItem { SourceItemId = SourceItemId, Title = "title" },
        CandidateInterests = [],
    };

    private static ClientContext CreateClient(
        StubHttpMessageHandler handler,
        IntelligenceApiOptions? options = null)
    {
        var httpClient = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = BaseAddress,
            Timeout = Timeout.InfiniteTimeSpan,
        };
        options ??= new IntelligenceApiOptions();
        var client = new HttpIntelligenceApiClient(
            httpClient,
            Options.Create(options),
            NullLogger<HttpIntelligenceApiClient>.Instance);
        return new ClientContext(client, httpClient, handler);
    }

    private sealed class ClientContext(HttpIntelligenceApiClient client, HttpClient httpClient, StubHttpMessageHandler handler) : IDisposable
    {
        public HttpIntelligenceApiClient Client { get; } = client;

        public void Dispose()
        {
            httpClient.Dispose();
            handler.Dispose();
        }
    }

    private const string AnalyzeRequestJson = """
    {
      "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "item": {
        "sourceItemId": "9b2e6f3a-1c4d-4e5a-8b7c-0d1e2f3a4b5c",
        "title": "New LTS runtime released",
        "excerpt": "A summary of the release.",
        "content": "Full article content about the release.",
        "language": "en"
      },
      "candidateInterests": [
        { "interestId": "0f8fad5b-d9cb-469f-a165-70867728950e", "name": "Runtime platforms", "priority": "high" },
        { "interestId": "7c9e6679-7425-40de-944b-e07fc1f90ae7", "name": "Security advisories", "priority": "medium" }
      ]
    }
    """;

    private const string AnalyzeResponseJson = """
    {
      "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "sourceItemId": "9b2e6f3a-1c4d-4e5a-8b7c-0d1e2f3a4b5c",
      "analyzerVersion": "analyze-1",
      "language": "en",
      "normalized": { "keywords": ["runtime", "lts", "release"], "eventDescriptors": ["version-release"] },
      "topics": ["runtime-platforms", "release-management"],
      "interestMatches": [
        { "interestId": "0f8fad5b-d9cb-469f-a165-70867728950e", "matchStrength": 0.92 },
        { "interestId": "7c9e6679-7425-40de-944b-e07fc1f90ae7", "matchStrength": 0.15 }
      ],
      "impact": { "level": "high", "confidence": 0.81 }
    }
    """;

    private const string AnalyzeResponseNoImpactJson = """
    {
      "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "sourceItemId": "9b2e6f3a-1c4d-4e5a-8b7c-0d1e2f3a4b5c",
      "analyzerVersion": "analyze-1",
      "language": null,
      "normalized": { "keywords": [], "eventDescriptors": [] },
      "topics": [],
      "interestMatches": [],
      "impact": { "level": "none", "confidence": 0.0 }
    }
    """;

    private const string SimilarityRequestJson = """
    {
      "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "candidate": { "text": "candidate update text" },
      "representatives": [
        { "updateId": "11111111-1111-1111-1111-111111111111", "text": "first representative" },
        { "updateId": "22222222-2222-2222-2222-222222222222", "text": "second representative" },
        { "updateId": "33333333-3333-3333-3333-333333333333", "text": "third representative" }
      ]
    }
    """;

    private const string SimilarityResponseJson = """
    {
      "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "algorithmVersion": "similarity-1",
      "results": [
        { "updateId": "11111111-1111-1111-1111-111111111111", "similarity": 1.0 },
        { "updateId": "22222222-2222-2222-2222-222222222222", "similarity": 0.4213 },
        { "updateId": "33333333-3333-3333-3333-333333333333", "similarity": 0.0 }
      ]
    }
    """;
}
