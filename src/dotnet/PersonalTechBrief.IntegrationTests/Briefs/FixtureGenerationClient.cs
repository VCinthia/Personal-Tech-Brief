using System.Collections.Concurrent;
using PersonalTechBrief.Infrastructure.Intelligence;
using PersonalTechBrief.Infrastructure.Intelligence.Contracts;

namespace PersonalTechBrief.IntegrationTests.Briefs;

/// <summary>
/// Deterministic fake <see cref="IIntelligenceApiClient"/> for brief integration tests. It generates a
/// stable title/summary/why-relevant from the request so no live LLM is required. Specific technology
/// update ids can be marked to fail persistently to exercise the exclude-on-failure path.
/// </summary>
public sealed class FixtureGenerationClient : IIntelligenceApiClient
{
    private readonly ConcurrentDictionary<Guid, byte> _failingUpdateIds = new();

    public string GenerationVersion { get; set; } = "generate-fixture-1";

    public void FailFor(Guid technologyUpdateId) => _failingUpdateIds[technologyUpdateId] = 1;

    public void Reset() => _failingUpdateIds.Clear();

    public Task<AnalyzeResponse> AnalyzeAsync(AnalyzeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Analyze is not exercised by brief tests.");

    public Task<SimilarityResponse> ComputeSimilarityAsync(SimilarityRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Similarity is not exercised by brief tests.");

    public Task<GenerateResponse> GenerateAsync(GenerateRequest request, CancellationToken cancellationToken)
    {
        if (_failingUpdateIds.ContainsKey(request.Update.TechnologyUpdateId))
        {
            throw new IntelligenceApiException(
                "The Intelligence API responded with status 502.",
                System.Net.HttpStatusCode.BadGateway,
                "Generation provider failed");
        }

        var primarySource = request.Update.Sources[0];
        return Task.FromResult(new GenerateResponse
        {
            CorrelationId = request.CorrelationId,
            TechnologyUpdateId = request.Update.TechnologyUpdateId,
            GenerationVersion = GenerationVersion,
            Title = $"Brief: {primarySource.Title}",
            Summary = $"Summary grounded in {request.Update.Sources.Count} source(s) for {request.Update.PrimaryTopic}.",
            WhyRelevant = $"Relevant to {request.MatchedInterests.Count} matched interest(s).",
        });
    }
}
