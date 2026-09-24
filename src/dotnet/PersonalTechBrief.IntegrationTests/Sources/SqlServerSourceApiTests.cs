using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTechBrief.Application.Sources;
using PersonalTechBrief.Domain.Sources;
using PersonalTechBrief.Infrastructure.Persistence;
using PersonalTechBrief.IntegrationTests.Interests;

namespace PersonalTechBrief.IntegrationTests.Sources;

public class SqlServerSourceApiTests(SqlServerInterestApiFixture fixture) : IClassFixture<SqlServerInterestApiFixture>
{
    [Fact]
    public async Task All_committed_migrations_drive_source_api_and_filtered_unique_behavior_through_sql_server()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/v1/sources",
            new { name = "Upper path", feedUrl = "https://feeds.example.test/Feed.xml?Case=Upper" });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var distinctCaseResponse = await client.PostAsJsonAsync(
            "/api/v1/sources",
            new { name = "Lower path", feedUrl = "HTTPS://FEEDS.EXAMPLE.TEST:443/feed.xml?Case=upper#fragment" });
        Assert.Equal(HttpStatusCode.Created, distinctCaseResponse.StatusCode);

        using var duplicateResponse = await client.PostAsJsonAsync(
            "/api/v1/sources",
            new { name = "Duplicate", feedUrl = "HTTPS://FEEDS.EXAMPLE.TEST:443/Feed.xml?Case=Upper#fragment" });
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Equal("application/problem+json", duplicateResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(2, factory.FeedValidator.ValidatedUris.Count);

        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<ISourceRepository>();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<ISourceRepository>();
        var now = DateTime.UtcNow;
        firstRepository.Add(Source.Create("Race one", "https://feeds.example.test/race.xml", now));
        secondRepository.Add(Source.Create("Race two", "https://feeds.example.test/race.xml", now));

        await firstRepository.SaveChangesAsync(CancellationToken.None);
        await Assert.ThrowsAsync<SourceConflictException>(() => secondRepository.SaveChangesAsync(CancellationToken.None));

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        Assert.Equal(3, await dbContext.Sources.CountAsync());
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        Assert.Contains("20260912231135_InitialCreate", appliedMigrations);
        Assert.Contains("20260913002225_AddSources", appliedMigrations);
        Assert.Contains("20260913004953_MakeSourceNormalizedFeedUrlCaseSensitive", appliedMigrations);
    }
}
