using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Infrastructure.Intelligence;
using PersonalTechBrief.Infrastructure.Intelligence.Contracts;

namespace PersonalTechBrief.UnitTests.Intelligence;

public class IntelligenceApiGenerateClientTests
{
    private static readonly Uri BaseAddress = new("https://intelligence.internal.test/");
    private static readonly Guid CorrelationId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private static readonly Guid TechnologyUpdateId = Guid.Parse("9b2e6f3a-1c4d-4e5a-8b7c-0d1e2f3a4b5c");

    [Fact]
    public async Task GenerateAsync_serializes_request_as_camelcase_matching_the_frozen_contract()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, GenerateResponseJson);
        using var context = CreateClient(handler);

        await context.Client.GenerateAsync(FrozenRequest(), CancellationToken.None);

        Assert.Equal("https://intelligence.internal.test/internal/v1/updates/generate", handler.RequestUris.Single()!.AbsoluteUri);
        var sent = JsonNode.Parse(handler.RequestBodies.Single()!)!;
        Assert.Equal("runtime-platforms", sent["update"]!["primaryTopic"]!.GetValue<string>());
        Assert.Equal("high", sent["matchedInterests"]![0]!["priority"]!.GetValue<string>());
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(GenerateRequestJson), sent),
            $"Generate request JSON drifted from the frozen contract. Actual: {sent}");
    }

    [Fact]
    public async Task GenerateAsync_deserializes_the_response()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, GenerateResponseJson);
        using var context = CreateClient(handler);

        var response = await context.Client.GenerateAsync(FrozenRequest(), CancellationToken.None);

        Assert.Equal(CorrelationId, response.CorrelationId);
        Assert.Equal(TechnologyUpdateId, response.TechnologyUpdateId);
        Assert.Equal("generate-1", response.GenerationVersion);
        Assert.Equal("Kubernetes 1.40 released with a critical runtime security fix", response.Title);
        Assert.Contains("runtime hardening", response.Summary, StringComparison.Ordinal);
        Assert.Contains("high priority", response.WhyRelevant, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "Malformed request body")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "Unprocessable content")]
    [InlineData(HttpStatusCode.BadGateway, "Generation provider failed")]
    [InlineData(HttpStatusCode.GatewayTimeout, "Generation provider timed out")]
    public async Task GenerateAsync_maps_problem_json_errors_to_typed_exception(HttpStatusCode statusCode, string title)
    {
        var problemJson = $$"""
        {"type":"https://intelligence.internal/errors/generate","title":"{{title}}","status":{{(int)statusCode}}}
        """;
        var handler = StubHttpMessageHandler.Returning(statusCode, problemJson, "application/problem+json");
        using var context = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<IntelligenceApiException>(
            () => context.Client.GenerateAsync(FrozenRequest(), CancellationToken.None));

        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Equal(title, exception.ProblemTitle);
        Assert.Equal(1, handler.CallCount); // received HTTP responses are not retried at the transport layer
    }

    [Fact]
    public async Task GenerateAsync_maps_configured_timeout_to_typed_exception()
    {
        var handler = new StubHttpMessageHandler(async (_, _, _, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var context = CreateClient(handler, new IntelligenceApiOptions { GenerateTimeoutSeconds = 0 });

        var exception = await Assert.ThrowsAsync<IntelligenceApiException>(
            () => context.Client.GenerateAsync(FrozenRequest(), CancellationToken.None));

        Assert.Null(exception.StatusCode);
        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static GenerateRequest FrozenRequest() => new()
    {
        CorrelationId = CorrelationId,
        Update = new GenerateUpdate
        {
            TechnologyUpdateId = TechnologyUpdateId,
            PrimaryTopic = "runtime-platforms",
            Sources =
            [
                new GenerateSource
                {
                    SourceItemId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"),
                    Title = "Kubernetes 1.40 released",
                    Excerpt = "A platform release with a critical runtime security fix.",
                    SourceUrl = "https://news.example.test/k8s-1-40",
                    PublishedAtUtc = new DateTime(2026, 9, 13, 15, 0, 0, DateTimeKind.Utc),
                },
                new GenerateSource
                {
                    SourceItemId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7"),
                    Title = "K8s 1.40 ships runtime hardening",
                    Excerpt = null,
                    SourceUrl = "https://feeds.example.test/k8s140",
                    PublishedAtUtc = null,
                },
            ],
        },
        MatchedInterests =
        [
            new GenerateMatchedInterest
            {
                InterestId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Platform engineering",
                Priority = InterestPriority.High,
            },
        ],
    };

    private static ClientContext CreateClient(StubHttpMessageHandler handler, IntelligenceApiOptions? options = null)
    {
        var httpClient = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = BaseAddress,
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var client = new HttpIntelligenceApiClient(
            httpClient,
            Options.Create(options ?? new IntelligenceApiOptions()),
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

    private const string GenerateRequestJson = """
    {
      "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "update": {
        "technologyUpdateId": "9b2e6f3a-1c4d-4e5a-8b7c-0d1e2f3a4b5c",
        "primaryTopic": "runtime-platforms",
        "sources": [
          {
            "sourceItemId": "0f8fad5b-d9cb-469f-a165-70867728950e",
            "title": "Kubernetes 1.40 released",
            "excerpt": "A platform release with a critical runtime security fix.",
            "sourceUrl": "https://news.example.test/k8s-1-40",
            "publishedAtUtc": "2026-09-13T15:00:00Z"
          },
          {
            "sourceItemId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
            "title": "K8s 1.40 ships runtime hardening",
            "excerpt": null,
            "sourceUrl": "https://feeds.example.test/k8s140",
            "publishedAtUtc": null
          }
        ]
      },
      "matchedInterests": [
        { "interestId": "11111111-1111-1111-1111-111111111111", "name": "Platform engineering", "priority": "high" }
      ]
    }
    """;

    private const string GenerateResponseJson = """
    {
      "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "technologyUpdateId": "9b2e6f3a-1c4d-4e5a-8b7c-0d1e2f3a4b5c",
      "generationVersion": "generate-1",
      "title": "Kubernetes 1.40 released with a critical runtime security fix",
      "summary": "Kubernetes 1.40 is out, delivering runtime hardening and a critical security fix reported by two sources.",
      "whyRelevant": "It affects the runtime platforms you follow at high priority and carries a critical security fix."
    }
    """;
}
