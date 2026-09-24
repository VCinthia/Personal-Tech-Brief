using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTechBrief.Application.Interests;
using PersonalTechBrief.Domain.Interests;
using PersonalTechBrief.Infrastructure.Persistence;

namespace PersonalTechBrief.IntegrationTests.Interests;

public class InterestApiTests(InterestApiFactory factory) : IClassFixture<InterestApiFactory>
{
    [Fact]
    public async Task Create_list_update_disable_and_read_workflow_persists_the_active_preference()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var createResponse = await client.PostAsJsonAsync(
            "/api/v1/interests",
            new { name = "  .NET  ", priority = "high" });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);

        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var interestId = created.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(".NET", created.RootElement.GetProperty("name").GetString());
        Assert.Equal("high", created.RootElement.GetProperty("priority").GetString());
        Assert.True(created.RootElement.GetProperty("isActive").GetBoolean());

        using var listResponse = await client.GetAsync("/api/v1/interests");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        Assert.Single(list.RootElement.EnumerateArray());

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/interests/{interestId}",
            new { name = ".NET", priority = "medium", isActive = true });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var updated = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        Assert.Equal("medium", updated.RootElement.GetProperty("priority").GetString());
        Assert.True(updated.RootElement.GetProperty("isActive").GetBoolean());

        using var activePreferenceScope = factory.Services.CreateScope();
        var dbContext = activePreferenceScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        var activeInterest = await dbContext.Interests.SingleAsync(interest => interest.Id == interestId);
        Assert.Equal(InterestPriority.Medium, activeInterest.Priority);
        Assert.True(activeInterest.IsActive);

        using var disableResponse = await client.DeleteAsync($"/api/v1/interests/{interestId}");
        Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);

        using var disabledResponse = await client.GetAsync($"/api/v1/interests/{interestId}");
        Assert.Equal(HttpStatusCode.OK, disabledResponse.StatusCode);
        using var disabled = JsonDocument.Parse(await disabledResponse.Content.ReadAsStringAsync());
        Assert.False(disabled.RootElement.GetProperty("isActive").GetBoolean());

        using var evaluationScope = factory.Services.CreateScope();
        var interestService = evaluationScope.ServiceProvider.GetRequiredService<IInterestService>();
        Assert.Empty(await interestService.ListActiveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Duplicate_active_normalized_name_returns_a_conflict_problem()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/v1/interests",
            new { name = "Azure", priority = "high" });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var duplicateResponse = await client.PostAsJsonAsync(
            "/api/v1/interests",
            new { name = "  azure ", priority = "low" });

        AssertProblem(duplicateResponse, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Invalid_and_absent_interest_requests_return_problem_details()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var invalidResponse = await client.PostAsJsonAsync(
            "/api/v1/interests",
            new { name = " ", priority = "not-a-priority" });
        AssertProblem(invalidResponse, HttpStatusCode.BadRequest);

        var absentId = Guid.NewGuid();
        using var getResponse = await client.GetAsync($"/api/v1/interests/{absentId}");
        AssertProblem(getResponse, HttpStatusCode.NotFound);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/interests/{absentId}",
            new { name = "Platform", priority = "high", isActive = true });
        AssertProblem(updateResponse, HttpStatusCode.NotFound);

        using var deleteResponse = await client.DeleteAsync($"/api/v1/interests/{absentId}");
        AssertProblem(deleteResponse, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Sql_server_model_contains_the_active_name_constraint_and_initial_migration()
    {
        var options = new DbContextOptionsBuilder<PersonalTechBriefDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PersonalTechBriefTests;Trusted_Connection=True")
            .Options;
        await using var dbContext = new PersonalTechBriefDbContext(options);

        var entityType = dbContext.Model.FindEntityType(typeof(Interest));
        var index = Assert.Single(entityType!.GetIndexes());

        Assert.True(index.IsUnique);
        Assert.Equal("[IsActive] = 1", index.GetFilter());
        Assert.Contains(dbContext.Database.GetMigrations(), migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
    }

    private static void AssertProblem(HttpResponseMessage response, HttpStatusCode expectedStatusCode)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
