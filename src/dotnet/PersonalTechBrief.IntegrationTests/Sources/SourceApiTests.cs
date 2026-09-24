using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using PersonalTechBrief.Application.Sources;
using PersonalTechBrief.Infrastructure.Persistence;

namespace PersonalTechBrief.IntegrationTests.Sources;

public class SourceApiTests(SourceApiFactory factory) : IClassFixture<SourceApiFactory>
{
    [Fact]
    public async Task Create_list_update_disable_enable_and_delete_preserve_source_traceability()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var createResponse = await client.PostAsJsonAsync(
            "/api/v1/sources",
            new { name = "  Engineering feed  ", feedUrl = "https://feeds.example.test/rss.xml" });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal("/api/v1/sources/", createResponse.Headers.Location!.OriginalString[..16]);

        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var sourceId = created.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("Engineering feed", created.RootElement.GetProperty("name").GetString());
        Assert.Equal("https://feeds.example.test/rss.xml", created.RootElement.GetProperty("feedUrl").GetString());
        Assert.True(created.RootElement.GetProperty("isEnabled").GetBoolean());
        Assert.Equal(JsonValueKind.Null, created.RootElement.GetProperty("lastIngestionAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, created.RootElement.GetProperty("lastIngestionStatus").ValueKind);
        Assert.Equal(
            ["feedUrl", "id", "isEnabled", "lastIngestionAtUtc", "lastIngestionStatus", "name"],
            created.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name));

        using var listResponse = await client.GetAsync("/api/v1/sources");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        Assert.Single(list.RootElement.EnumerateArray());

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/sources/{sourceId}",
            new { name = "Engineering news", feedUrl = "https://feeds.example.test/rss.xml", isEnabled = false });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        using var updated = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        Assert.False(updated.RootElement.GetProperty("isEnabled").GetBoolean());

        using var enableResponse = await client.PostAsync($"/api/v1/sources/{sourceId}/enable", content: null);
        Assert.Equal(HttpStatusCode.NoContent, enableResponse.StatusCode);
        Assert.Contains(factory.FeedValidator.ValidatedUris, uri => uri.Host == "feeds.example.test");

        using var disableResponse = await client.PostAsync($"/api/v1/sources/{sourceId}/disable", content: null);
        Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);

        using var deleteResponse = await client.DeleteAsync($"/api/v1/sources/{sourceId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var disabledResponse = await client.GetAsync($"/api/v1/sources/{sourceId}");
        Assert.Equal(HttpStatusCode.OK, disabledResponse.StatusCode);
        using var disabled = JsonDocument.Parse(await disabledResponse.Content.ReadAsStringAsync());
        Assert.False(disabled.RootElement.GetProperty("isEnabled").GetBoolean());

        using var sourceScope = factory.Services.CreateScope();
        var sourceService = sourceScope.ServiceProvider.GetRequiredService<ISourceService>();
        Assert.Empty(await sourceService.ListEnabledAsync(CancellationToken.None));

        var dbContext = sourceScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        var persistedSource = await dbContext.Sources.SingleAsync(source => source.Id == sourceId);
        Assert.False(persistedSource.IsEnabled);
    }

    [Fact]
    public async Task Active_normalized_url_collision_returns_a_conflict_problem()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/v1/sources",
            new { name = "Platform", feedUrl = "https://feeds.example.test/rss.xml" });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var duplicateResponse = await client.PostAsJsonAsync(
            "/api/v1/sources",
            new { name = "Duplicate", feedUrl = "HTTPS://FEEDS.EXAMPLE.TEST:443/rss.xml#fragment" });

        AssertProblem(duplicateResponse, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Invalid_source_shapes_and_absent_resources_return_problem_details()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var invalidResponse = await client.PostAsJsonAsync(
            "/api/v1/sources",
            new { name = "", feedUrl = "http://127.0.0.1/feed.xml" });
        AssertProblem(invalidResponse, HttpStatusCode.BadRequest);

        var absentId = Guid.NewGuid();
        using var getResponse = await client.GetAsync($"/api/v1/sources/{absentId}");
        AssertProblem(getResponse, HttpStatusCode.NotFound);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/sources/{absentId}",
            new { name = "Feed", feedUrl = "https://feeds.example.test/rss.xml", isEnabled = true });
        AssertProblem(updateResponse, HttpStatusCode.NotFound);

        using var deleteResponse = await client.DeleteAsync($"/api/v1/sources/{absentId}");
        AssertProblem(deleteResponse, HttpStatusCode.NotFound);

        using var enableResponse = await client.PostAsync($"/api/v1/sources/{absentId}/enable", content: null);
        AssertProblem(enableResponse, HttpStatusCode.NotFound);

        using var disableResponse = await client.PostAsync($"/api/v1/sources/{absentId}/disable", content: null);
        AssertProblem(disableResponse, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_url_that_expands_past_the_persistence_bound_returns_400_without_feed_validation()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var feedUrl = $"https://feeds.example.test/{new string('あ', 100)}";

        using var response = await client.PostAsJsonAsync(
            "/api/v1/sources",
            new { name = "Too long after escaping", feedUrl });

        AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Empty(factory.FeedValidator.ValidatedUris);

        using var sourceScope = factory.Services.CreateScope();
        var dbContext = sourceScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        Assert.Empty(await dbContext.Sources.ToListAsync());
    }

    [Fact]
    public async Task An_uninterpretable_feed_returns_422_and_is_never_persisted_or_activated()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var createResponse = await client.PostAsJsonAsync(
            "/api/v1/sources",
            new { name = "Uninterpretable", feedUrl = "https://invalid-feed.test/bad.xml" });

        AssertProblem(createResponse, HttpStatusCode.UnprocessableEntity);

        using var sourceScope = factory.Services.CreateScope();
        var dbContext = sourceScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        Assert.Empty(await dbContext.Sources.ToListAsync());
    }

    [Fact]
    public async Task A_failed_revalidation_does_not_persist_an_updated_feed_url_or_activate_the_source()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();
        var sourceId = await CreateSourceAsync(client, "Validated", "https://feeds.example.test/rss.xml");

        using var disableResponse = await client.PostAsync($"/api/v1/sources/{sourceId}/disable", content: null);
        Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/sources/{sourceId}",
            new { name = "Validated", feedUrl = "https://invalid-feed.test/bad.xml", isEnabled = true });
        AssertProblem(updateResponse, HttpStatusCode.UnprocessableEntity);

        using var getResponse = await client.GetAsync($"/api/v1/sources/{sourceId}");
        getResponse.EnsureSuccessStatusCode();
        using var source = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        Assert.False(source.RootElement.GetProperty("isEnabled").GetBoolean());
        Assert.Equal("https://feeds.example.test/rss.xml", source.RootElement.GetProperty("feedUrl").GetString());
    }

    [Fact]
    public async Task Sql_server_model_contains_the_case_sensitive_active_url_constraint_and_source_migrations()
    {
        var options = new DbContextOptionsBuilder<PersonalTechBriefDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PersonalTechBriefTests;Trusted_Connection=True")
            .Options;
        await using var dbContext = new PersonalTechBriefDbContext(options);

        var entityType = dbContext.Model.FindEntityType(typeof(PersonalTechBrief.Domain.Sources.Source));
        var index = Assert.Single(entityType!.GetIndexes());

        Assert.True(index.IsUnique);
        Assert.Equal("[IsEnabled] = 1", index.GetFilter());
        Assert.Equal(850, entityType.FindProperty("NormalizedFeedUrl")!.GetMaxLength());
        var designTimeEntityType = dbContext.GetService<IDesignTimeModel>()
            .Model
            .FindEntityType(typeof(PersonalTechBrief.Domain.Sources.Source));
        Assert.Equal("Latin1_General_100_BIN2", designTimeEntityType!.FindProperty("NormalizedFeedUrl")!.GetCollation());
        Assert.Contains(dbContext.Database.GetMigrations(), migration => migration.EndsWith("_AddSources", StringComparison.Ordinal));
        Assert.Contains(dbContext.Database.GetMigrations(), migration => migration.EndsWith("_MakeSourceNormalizedFeedUrlCaseSensitive", StringComparison.Ordinal));
    }

    private static async Task<Guid> CreateSourceAsync(HttpClient client, string name, string feedUrl)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/sources", new { name, feedUrl });
        response.EnsureSuccessStatusCode();
        using var source = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return source.RootElement.GetProperty("id").GetGuid();
    }

    private static void AssertProblem(HttpResponseMessage response, HttpStatusCode expectedStatusCode)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
