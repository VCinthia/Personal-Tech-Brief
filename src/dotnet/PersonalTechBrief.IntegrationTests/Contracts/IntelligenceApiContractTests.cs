using System.Text.Json;
using System.Text.Json.Nodes;
using PersonalTechBrief.Infrastructure.Intelligence;
using PersonalTechBrief.Infrastructure.Intelligence.Contracts;

namespace PersonalTechBrief.IntegrationTests.Contracts;

/// <summary>
/// Cross-language contract tests. They pin the .NET Intelligence API DTOs to the language-neutral
/// canonical JSON fixtures derived from docs/architecture/intelligence-api.md, so any drift in
/// field names, casing, enum values, [0,1] ranges, required fields, or similarity ordering is
/// caught in CI without a live Python service. The JSON fixtures are the shared source of truth.
/// </summary>
public class IntelligenceApiContractTests
{
    [Theory]
    [InlineData("analyze-request.json")]
    [InlineData("analyze-response.json")]
    [InlineData("analyze-response-none.json")]
    [InlineData("similarity-request.json")]
    [InlineData("similarity-response.json")]
    [InlineData("similarity-response-empty.json")]
    [InlineData("generate-request.json")]
    [InlineData("generate-request-maxlengths.json")]
    [InlineData("generate-response.json")]
    public void Canonical_payloads_round_trip_through_the_dotnet_dtos(string fixtureName)
    {
        var canonical = ReadFixture(fixtureName);
        object dto = Deserialize(fixtureName, canonical);
        var reserialized = JsonSerializer.Serialize(dto, dto.GetType(), IntelligenceApiJson.Options);

        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(canonical), JsonNode.Parse(reserialized)),
            $"The .NET DTO round-trip of {fixtureName} diverged from the canonical contract fixture. Actual: {reserialized}");
    }

    [Fact]
    public void Analyze_request_maps_priority_enum_values_to_the_contract_spelling()
    {
        var request = JsonSerializer.Deserialize<AnalyzeRequest>(ReadFixture("analyze-request.json"), IntelligenceApiJson.Options)!;

        Assert.Equal(Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"), request.CorrelationId);
        Assert.Equal(InterestPriority.High, request.CandidateInterests[0].Priority);
        Assert.Equal(InterestPriority.Medium, request.CandidateInterests[1].Priority);
    }

    [Fact]
    public void Analyze_response_deserializes_enums_and_bounded_floats()
    {
        var response = JsonSerializer.Deserialize<AnalyzeResponse>(ReadFixture("analyze-response.json"), IntelligenceApiJson.Options)!;

        Assert.Equal(ImpactLevel.High, response.Impact.Level);
        Assert.InRange(response.Impact.Confidence, 0.0, 1.0);
        Assert.All(response.InterestMatches, match => Assert.InRange(match.MatchStrength, 0.0, 1.0));
    }

    [Fact]
    public void Similarity_response_preserves_representative_order_and_bounds()
    {
        var response = JsonSerializer.Deserialize<SimilarityResponse>(ReadFixture("similarity-response.json"), IntelligenceApiJson.Options)!;

        Assert.Equal(
            [
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ],
            response.Results.Select(result => result.UpdateId));
        Assert.Equal(1.0, response.Results[0].Similarity);
        Assert.Equal(0.0, response.Results[2].Similarity);
        Assert.All(response.Results, result => Assert.InRange(result.Similarity, 0.0, 1.0));
    }

    [Fact]
    public void Generate_request_binds_priority_enum_and_nullable_source_fields()
    {
        var request = JsonSerializer.Deserialize<GenerateRequest>(ReadFixture("generate-request.json"), IntelligenceApiJson.Options)!;

        Assert.Equal(Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"), request.CorrelationId);
        Assert.Equal("runtime-platforms", request.Update.PrimaryTopic);
        Assert.Equal(2, request.Update.Sources.Count);
        Assert.Null(request.Update.Sources[1].Excerpt);
        Assert.Null(request.Update.Sources[1].PublishedAtUtc);
        Assert.Equal(InterestPriority.High, request.MatchedInterests[0].Priority);
    }

    [Fact]
    public void Generate_response_binds_the_frozen_fields()
    {
        var response = JsonSerializer.Deserialize<GenerateResponse>(ReadFixture("generate-response.json"), IntelligenceApiJson.Options)!;

        Assert.Equal(Guid.Parse("9b2e6f3a-1c4d-4e5a-8b7c-0d1e2f3a4b5c"), response.TechnologyUpdateId);
        Assert.Equal("generate-1", response.GenerationVersion);
        Assert.False(string.IsNullOrWhiteSpace(response.Title));
        Assert.False(string.IsNullOrWhiteSpace(response.Summary));
        Assert.False(string.IsNullOrWhiteSpace(response.WhyRelevant));
    }

    [Fact]
    public void Generate_response_missing_required_field_is_rejected_so_drift_is_caught()
    {
        var node = JsonNode.Parse(ReadFixture("generate-response.json"))!.AsObject();
        node.Remove("generationVersion");

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<GenerateResponse>(node.ToJsonString(), IntelligenceApiJson.Options));
    }

    [Fact]
    public void Missing_required_field_is_rejected_so_contract_drift_is_caught()
    {
        // Drop the required analyzerVersion field: deserialization must fail rather than silently succeed.
        var node = JsonNode.Parse(ReadFixture("analyze-response.json"))!.AsObject();
        node.Remove("analyzerVersion");

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<AnalyzeResponse>(node.ToJsonString(), IntelligenceApiJson.Options));
    }

    [Fact]
    public void Pascal_case_field_casing_is_rejected_so_casing_drift_is_caught()
    {
        // The contract is camelCase; a PascalCase field must not bind to a required member.
        const string pascalCased = """
        {
          "CorrelationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "AlgorithmVersion": "similarity-1",
          "Results": []
        }
        """;

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<SimilarityResponse>(pascalCased, IntelligenceApiJson.Options));
    }

    private static object Deserialize(string fixtureName, string json) => fixtureName switch
    {
        "analyze-request.json" => JsonSerializer.Deserialize<AnalyzeRequest>(json, IntelligenceApiJson.Options)!,
        "analyze-response.json" or "analyze-response-none.json" => JsonSerializer.Deserialize<AnalyzeResponse>(json, IntelligenceApiJson.Options)!,
        "similarity-request.json" => JsonSerializer.Deserialize<SimilarityRequest>(json, IntelligenceApiJson.Options)!,
        "similarity-response.json" or "similarity-response-empty.json" => JsonSerializer.Deserialize<SimilarityResponse>(json, IntelligenceApiJson.Options)!,
        "generate-request.json" or "generate-request-maxlengths.json" => JsonSerializer.Deserialize<GenerateRequest>(json, IntelligenceApiJson.Options)!,
        "generate-response.json" => JsonSerializer.Deserialize<GenerateResponse>(json, IntelligenceApiJson.Options)!,
        _ => throw new ArgumentOutOfRangeException(nameof(fixtureName), fixtureName, "Unknown contract fixture."),
    };

    private static string ReadFixture(string fixtureName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "Fixtures", "Intelligence", fixtureName));
}
