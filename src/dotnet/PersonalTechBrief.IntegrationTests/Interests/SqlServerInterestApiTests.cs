using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTechBrief.Application.Interests;
using PersonalTechBrief.Domain.Interests;
using PersonalTechBrief.Infrastructure.Persistence;

namespace PersonalTechBrief.IntegrationTests.Interests;

public class SqlServerInterestApiTests(SqlServerInterestApiFixture fixture) : IClassFixture<SqlServerInterestApiFixture>
{
    [Fact]
    public async Task Committed_migration_drives_the_interests_api_and_readiness_through_sql_server()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var readinessResponse = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, readinessResponse.StatusCode);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/v1/interests",
            new { name = "SQL Server", priority = "high" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var listResponse = await client.GetAsync("/api/v1/interests");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Contains("SQL Server", await listResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        Assert.Equal(1, await dbContext.Interests.CountAsync());
        Assert.Contains("20260912231135_InitialCreate", await dbContext.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Sql_server_filtered_unique_index_translates_a_racing_duplicate_save_to_an_api_conflict()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();

        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<IInterestRepository>();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<IInterestRepository>();
        var now = DateTime.UtcNow;

        firstRepository.Add(Interest.Create("Azure SQL", InterestPriority.High, now));
        secondRepository.Add(Interest.Create(" azure sql ", InterestPriority.Low, now));

        await firstRepository.SaveChangesAsync(CancellationToken.None);
        await Assert.ThrowsAsync<InterestConflictException>(
            () => secondRepository.SaveChangesAsync(CancellationToken.None));

        using var client = factory.CreateClient();
        using var conflictResponse = await client.PostAsJsonAsync(
            "/api/v1/interests",
            new { name = "AZURE SQL", priority = "medium" });

        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        Assert.Equal("application/problem+json", conflictResponse.Content.Headers.ContentType?.MediaType);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        Assert.Equal(1, await dbContext.Interests.CountAsync());
    }
}
