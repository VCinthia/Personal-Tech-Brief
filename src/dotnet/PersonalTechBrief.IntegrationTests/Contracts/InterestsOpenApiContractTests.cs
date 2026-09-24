using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using PersonalTechBrief.IntegrationTests.Interests;

namespace PersonalTechBrief.IntegrationTests.Contracts;

public class PublicApiOpenApiContractTests(InterestApiFactory factory) : IClassFixture<InterestApiFactory>
{
    [Fact]
    public async Task Generated_public_api_contract_matches_the_committed_baseline_and_problem_details_behavior()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var openApiResponse = await client.GetAsync("/openapi/v1.json");
        openApiResponse.EnsureSuccessStatusCode();

        var generatedContract = JsonNode.Parse(await openApiResponse.Content.ReadAsStringAsync());
        var committedContract = JsonNode.Parse(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "contracts", "public-api.openapi.json")));

        Assert.NotNull(generatedContract);
        Assert.NotNull(committedContract);
        generatedContract.AsObject().Remove("servers");
        committedContract.AsObject().Remove("servers");
        Assert.True(
            JsonNode.DeepEquals(committedContract, generatedContract),
            "The generated OpenAPI contract changed. Update docs/contracts/public-api.openapi.json intentionally.");

        var paths = generatedContract["paths"]!.AsObject();
        Assert.Equal(
            [
                "/api/v1/articles",
                "/api/v1/briefs",
                "/api/v1/briefs/current",
                "/api/v1/briefs/{id}",
                "/api/v1/evaluation/summary",
                "/api/v1/interests",
                "/api/v1/interests/{id}",
                "/api/v1/sources",
                "/api/v1/sources/{id}",
                "/api/v1/sources/{id}/disable",
                "/api/v1/sources/{id}/enable",
                "/api/v1/updates/{id}/feedback",
                "/api/v1/updates/{id}/saved",
                "/api/v1/updates/{updateId}/sources/{sourceItemId}/open",
            ],
            paths.Select(path => path.Key).OrderBy(path => path, StringComparer.Ordinal));
        AssertOperation(paths, "/api/v1/articles", "post", "202", "400", "422");
        AssertOperation(paths, "/api/v1/briefs", "get", "200");
        AssertOperation(paths, "/api/v1/briefs", "post", "202");
        AssertOperation(paths, "/api/v1/briefs/current", "get", "200", "404");
        AssertOperation(paths, "/api/v1/briefs/{id}", "get", "200", "404");
        AssertOperation(paths, "/api/v1/interests", "get", "200");
        AssertOperation(paths, "/api/v1/interests", "post", "201", "400", "409");
        AssertOperation(paths, "/api/v1/interests/{id}", "get", "200", "404");
        AssertOperation(paths, "/api/v1/interests/{id}", "put", "200", "400", "404", "409");
        AssertOperation(paths, "/api/v1/interests/{id}", "delete", "204", "404");
        AssertOperation(paths, "/api/v1/sources", "get", "200");
        AssertOperation(paths, "/api/v1/sources", "post", "201", "400", "409", "422");
        AssertOperation(paths, "/api/v1/sources/{id}", "get", "200", "404");
        AssertOperation(paths, "/api/v1/sources/{id}", "put", "200", "400", "404", "409", "422");
        AssertOperation(paths, "/api/v1/sources/{id}", "delete", "204", "404");
        AssertOperation(paths, "/api/v1/sources/{id}/enable", "post", "204", "404", "409", "422");
        AssertOperation(paths, "/api/v1/sources/{id}/disable", "post", "204", "404");
        AssertOperation(paths, "/api/v1/updates/{id}/feedback", "put", "204", "400", "404");
        AssertOperation(paths, "/api/v1/updates/{id}/feedback", "delete", "204");
        AssertOperation(paths, "/api/v1/updates/{id}/saved", "put", "204", "404");
        AssertOperation(paths, "/api/v1/updates/{id}/saved", "delete", "204");
        AssertOperation(paths, "/api/v1/updates/{updateId}/sources/{sourceItemId}/open", "post", "202", "404");
        AssertOperation(paths, "/api/v1/evaluation/summary", "get", "200");

        using var invalidResponse = await client.PostAsJsonAsync(
            "/api/v1/interests",
            new { name = " ", priority = "not-a-priority" });
        AssertProblem(invalidResponse, HttpStatusCode.BadRequest);

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/v1/interests",
            new { name = "OpenAPI", priority = "high" });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var duplicateResponse = await client.PostAsJsonAsync(
            "/api/v1/interests",
            new { name = " openapi ", priority = "low" });
        AssertProblem(duplicateResponse, HttpStatusCode.Conflict);
    }

    private static void AssertOperation(JsonObject paths, string path, string method, params string[] statusCodes)
    {
        var operation = paths[path]![method]!.AsObject();
        var responses = operation["responses"]!.AsObject();

        Assert.Equal(statusCodes.OrderBy(statusCode => statusCode), responses.Select(response => response.Key).OrderBy(statusCode => statusCode));
    }

    private static void AssertProblem(HttpResponseMessage response, HttpStatusCode expectedStatusCode)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
