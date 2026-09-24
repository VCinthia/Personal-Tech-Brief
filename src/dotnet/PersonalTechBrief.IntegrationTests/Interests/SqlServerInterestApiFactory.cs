using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PersonalTechBrief.Application.Sources;
using PersonalTechBrief.IntegrationTests.Briefs;
using PersonalTechBrief.IntegrationTests.Sources;
using PersonalTechBrief.Infrastructure.Intelligence;
using PersonalTechBrief.Infrastructure.Persistence;

namespace PersonalTechBrief.IntegrationTests.Interests;

public sealed class SqlServerInterestApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public FixtureFeedValidator FeedValidator { get; } = new();

    /// <summary>Deterministic fake for the internal generate endpoint, so brief tests never call a live LLM.</summary>
    public FixtureGenerationClient GenerationClient { get; } = new();

    /// <summary>The disposable SQL Server connection string, for tests that build their own container.</summary>
    public string ConnectionString => connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PersonalTechBriefDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<IDbContextOptionsConfiguration<PersonalTechBriefDbContext>>();
            services.RemoveAll<PersonalTechBriefDbContext>();
            services.AddDbContext<PersonalTechBriefDbContext>(options =>
                options.UseSqlServer(
                    connectionString,
                    sqlServer => sqlServer.MigrationsAssembly(typeof(PersonalTechBriefDbContext).Assembly.FullName)));

            services.RemoveAll<IFeedValidator>();
            services.AddSingleton<IFeedValidator>(FeedValidator);

            // Replace the HTTP-backed Intelligence client so brief generation uses a deterministic fake.
            services.RemoveAll<IIntelligenceApiClient>();
            services.AddSingleton<IIntelligenceApiClient>(GenerationClient);
        });
    }

    public async Task MigrateDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();

        await dbContext.Database.MigrateAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();

        // Interaction tables first: their restrictive FKs to TechnologyUpdates, SourceItems and BriefItems
        // mean they must be cleared before any of those referenced tables below.
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [UserFeedback]");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [SavedUpdates]");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [SourceOpenEvents]");

        // Brief tables next: their FKs to SourceItems and TechnologyUpdates are restrictive, so they
        // must be cleared before those referenced tables below.
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [BriefItemSources]");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [BriefItems]");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [Briefs]");

        // Grouping tables next: their FKs to SourceItems and Interests are restrictive, so they
        // must be cleared before the tables they reference below.
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [UpdateInterestMatches]");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [TechnologyUpdateSources]");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [TechnologyUpdates]");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [OutboxMessages]");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [ContentProcessingInbox]");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [SourceItems]");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [IngestionRuns]");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [Sources]");
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM [Interests]");
        FeedValidator.Reset();
    }
}
