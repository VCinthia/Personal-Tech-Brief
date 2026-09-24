using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalTechBrief.Application.Analysis;
using PersonalTechBrief.Application.Ingestion;
using PersonalTechBrief.Application.Messaging;
using PersonalTechBrief.Application.Sources;
using PersonalTechBrief.Domain.Interests;
using PersonalTechBrief.Domain.Sources;
using PersonalTechBrief.Infrastructure;
using PersonalTechBrief.Infrastructure.Analysis;
using PersonalTechBrief.Infrastructure.Intelligence;
using PersonalTechBrief.Infrastructure.Intelligence.Contracts;
using PersonalTechBrief.Infrastructure.Messaging;
using PersonalTechBrief.Infrastructure.Persistence;
using PersonalTechBrief.IntegrationTests.Interests;
using InterestPriority = PersonalTechBrief.Domain.Interests.InterestPriority;

namespace PersonalTechBrief.IntegrationTests.Analysis;

// Real-SQL coverage for grouping and relevance (FR-007/008/009/010/017): the migration applies, a new
// item creates one update with one association and interest matches, a similar item from another host
// merges (unique pair holds, second association added, score recomputed, host count drives support),
// and reprocessing the same receipt is a no-op. Python is a fake provider — no live LLM.
public sealed class SqlServerGroupingPipelineTests(SqlServerInterestApiFixture fixture)
    : IClassFixture<SqlServerInterestApiFixture>
{
    private static readonly DateTime FixedNowUtc = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task New_item_creates_one_update_one_association_and_interest_matches()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();

        var intelligence = new ConfigurableIntelligenceClient();
        await using var provider = BuildProvider(factory.ConnectionString, intelligence);

        var interestId = await SeedInterestAsync(provider);
        intelligence.Configure(interestId, matchStrength: 1.0, topic: "platform");

        var seeded = await SeedItemAsync(provider, "https://feeds.a.test/rss.xml", "item-a", FixedNowUtc.AddHours(-1));
        var pipeline = provider.GetRequiredService<IGroupingPipelineService>();
        var outcome = await pipeline.ProcessAsync(seeded.Envelope, CancellationToken.None);

        Assert.Equal(GroupingProcessingOutcome.Grouped, outcome);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        Assert.Equal(1, await db.TechnologyUpdates.CountAsync());
        Assert.Equal(1, await db.TechnologyUpdateSources.CountAsync(a => a.SourceItemId == seeded.SourceItemId));
        Assert.Equal(1, await db.UpdateInterestMatches.CountAsync(m => m.InterestId == interestId));
        Assert.Equal(
            Domain.Ingestion.SourceItemProcessingStatus.Processed,
            await db.SourceItems.Where(i => i.Id == seeded.SourceItemId).Select(i => i.ProcessingStatus).SingleAsync());
        Assert.Equal(
            Domain.Messaging.ContentProcessingInboxStatus.Processed,
            await db.ContentProcessingInbox.Where(r => r.SourceItemId == seeded.SourceItemId).Select(r => r.Status).SingleAsync());

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(appliedMigrations, migration => migration.EndsWith("AddGroupingAndRelevance", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Similar_item_from_another_host_merges_and_recomputes_score_with_source_support()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();

        var intelligence = new ConfigurableIntelligenceClient { SimilarityValue = 0.9 };
        await using var provider = BuildProvider(factory.ConnectionString, intelligence);

        var interestId = await SeedInterestAsync(provider);
        intelligence.Configure(interestId, matchStrength: 1.0, topic: "platform");
        var pipeline = provider.GetRequiredService<IGroupingPipelineService>();

        var first = await SeedItemAsync(provider, "https://feeds.a.test/rss.xml", "item-a", FixedNowUtc.AddHours(-1));
        await pipeline.ProcessAsync(first.Envelope, CancellationToken.None);
        var scoreAfterFirst = await ReadUpdateScoreAsync(provider);

        var second = await SeedItemAsync(provider, "https://feeds.b.test/rss.xml", "item-b", FixedNowUtc.AddMinutes(-30));
        var outcome = await pipeline.ProcessAsync(second.Envelope, CancellationToken.None);

        Assert.Equal(GroupingProcessingOutcome.Grouped, outcome);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();

        // Merged, not duplicated: still one update, now with both source associations (unique pair holds).
        var updateId = await db.TechnologyUpdates.Select(u => u.Id).SingleAsync();
        Assert.Equal(2, await db.TechnologyUpdateSources.CountAsync(a => a.TechnologyUpdateId == updateId));
        var mergedAssociation = await db.TechnologyUpdateSources
            .SingleAsync(a => a.SourceItemId == second.SourceItemId);
        Assert.Equal(updateId, mergedAssociation.TechnologyUpdateId);
        Assert.Equal(0.9d, mergedAssociation.SimilarityScore!.Value, 5);

        // Both items are within 24h and match the High interest at 1.0, so interest (60) and recency (20)
        // are unchanged; the only delta is source support rising from a single host (0) to two hosts (+4).
        var scoreAfterMerge = await ReadUpdateScoreAsync(provider);
        Assert.Equal(80d, scoreAfterFirst, 5);
        Assert.Equal(84d, scoreAfterMerge, 5);
    }

    [Fact]
    public async Task Reprocessing_the_same_receipt_is_a_no_op()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();

        var intelligence = new ConfigurableIntelligenceClient();
        await using var provider = BuildProvider(factory.ConnectionString, intelligence);

        var interestId = await SeedInterestAsync(provider);
        intelligence.Configure(interestId, matchStrength: 1.0, topic: "platform");
        var pipeline = provider.GetRequiredService<IGroupingPipelineService>();

        var seeded = await SeedItemAsync(provider, "https://feeds.a.test/rss.xml", "item-a", FixedNowUtc.AddHours(-1));
        var first = await pipeline.ProcessAsync(seeded.Envelope, CancellationToken.None);
        var replay = await pipeline.ProcessAsync(seeded.Envelope, CancellationToken.None);

        Assert.Equal(GroupingProcessingOutcome.Grouped, first);
        Assert.Equal(GroupingProcessingOutcome.SkippedAlreadyHandled, replay);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        Assert.Equal(1, await db.TechnologyUpdates.CountAsync());
        Assert.Equal(1, await db.TechnologyUpdateSources.CountAsync());
    }

    [Fact]
    public async Task Recording_a_failure_after_a_premature_in_memory_processed_mutation_still_increments_retry_count()
    {
        // Regression for the mid-commit failure path: if a grouping commit fails after it already
        // called SourceItem.MarkProcessed() in memory (rolled back in the DB, never saved), recording
        // the failure on the same DbContext must still observe the committed non-terminal state and
        // increment the bounded-retry FailureCount — not short-circuit on the phantom Processed status.
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();

        var intelligence = new ConfigurableIntelligenceClient();
        await using var provider = BuildProvider(factory.ConnectionString, intelligence);
        var interestId = await SeedInterestAsync(provider);
        intelligence.Configure(interestId, matchStrength: 1.0, topic: "platform");
        var seeded = await SeedItemAsync(provider, "https://feeds.a.test/rss.xml", "item-a", FixedNowUtc.AddHours(-1));

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IGroupingRepository>();

        // Reproduce the dirty change-tracker: the SourceItem is tracked with a premature Processed status.
        var tracked = await db.SourceItems.FirstAsync(item => item.Id == seeded.SourceItemId);
        tracked.MarkProcessed();
        Assert.Equal(Domain.Ingestion.SourceItemProcessingStatus.Processed, tracked.ProcessingStatus);

        var failureCount = await repository.RecordProcessingFailureAsync(
            seeded.SourceItemId, "transient_test", terminal: false, CancellationToken.None);

        Assert.Equal(1, failureCount);
        db.ChangeTracker.Clear();
        var persisted = await db.SourceItems.AsNoTracking().SingleAsync(item => item.Id == seeded.SourceItemId);
        Assert.Equal(Domain.Ingestion.SourceItemProcessingStatus.FailedRetryable, persisted.ProcessingStatus);
        Assert.Equal(1, persisted.FailureCount);
    }

    private static ServiceProvider BuildProvider(string connectionString, IIntelligenceApiClient intelligence)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PersonalTechBrief"] = connectionString,
                ["ServiceBus:ConnectionString"] = "Endpoint=sb://placeholder.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=placeholder",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddInfrastructure(configuration);
        services.AddMessagingInfrastructure(configuration);
        services.AddGroupingAndRelevance(configuration);
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedNowUtc));
        services.AddSingleton(intelligence);

        return services.BuildServiceProvider();
    }

    private static async Task<Guid> SeedInterestAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<Application.Interests.IInterestRepository>();
        var interest = Interest.Create("Platform engineering", InterestPriority.High, FixedNowUtc.AddDays(-1));
        repository.Add(interest);
        await repository.SaveChangesAsync(CancellationToken.None);
        return interest.Id;
    }

    private static async Task<SeededItem> SeedItemAsync(
        ServiceProvider provider,
        string feedUrl,
        string externalId,
        DateTime publishedAtUtc)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var sourceRepository = services.GetRequiredService<ISourceRepository>();
        var source = Source.Create($"Feed {externalId}", feedUrl, FixedNowUtc.AddDays(-2));
        sourceRepository.Add(source);
        await sourceRepository.SaveChangesAsync(CancellationToken.None);

        var runService = services.GetRequiredService<IIngestionRunService>();
        var run = await runService.StartAsync(
            new StartIngestionRunCommand(source.Id, Guid.NewGuid(), FixedNowUtc.AddHours(-2)),
            CancellationToken.None);

        var persistence = services.GetRequiredService<ISourceItemPersistenceService>();
        var persisted = await persistence.PersistIfNewAsync(
            new PersistSourceItemCommand(
                source.Id,
                run.IngestionRunId,
                Guid.NewGuid(),
                externalId,
                $"https://articles.example.test/{externalId}",
                "Kubernetes 1.40 released",
                "A platform release",
                publishedAtUtc,
                $"hash-{externalId}",
                publishedAtUtc.AddMinutes(5),
                "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01"),
            CancellationToken.None);
        Assert.False(persisted.IsDuplicate);
        var sourceItemId = persisted.SourceItemId!.Value;

        var db = services.GetRequiredService<PersonalTechBriefDbContext>();
        var payload = await db.OutboxMessages.AsNoTracking()
            .Where(message => message.SourceItemId == sourceItemId)
            .Select(message => message.Payload)
            .SingleAsync();
        var envelope = System.Text.Json.JsonSerializer.Deserialize<SourceItemReadyEnvelope>(
            payload, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        var inbox = services.GetRequiredService<IContentProcessingInboxStore>();
        var acceptance = await inbox.AcceptAsync(envelope, FixedNowUtc.AddMinutes(-10), CancellationToken.None);
        Assert.Equal(ContentProcessingInboxAcceptance.Accepted, acceptance);

        return new SeededItem(sourceItemId, envelope);
    }

    private static async Task<double> ReadUpdateScoreAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        return await db.TechnologyUpdates.AsNoTracking().Select(u => u.CurrentRelevanceScore).SingleAsync();
    }

    private sealed record SeededItem(Guid SourceItemId, SourceItemReadyEnvelope Envelope);

    private sealed class ConfigurableIntelligenceClient : IIntelligenceApiClient
    {
        private Guid interestId;
        private double matchStrength;
        private string topic = "platform";

        public double SimilarityValue { get; init; } = 0.1;

        public void Configure(Guid interestId, double matchStrength, string topic)
        {
            this.interestId = interestId;
            this.matchStrength = matchStrength;
            this.topic = topic;
        }

        public Task<AnalyzeResponse> AnalyzeAsync(AnalyzeRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AnalyzeResponse
            {
                CorrelationId = request.CorrelationId,
                SourceItemId = request.Item.SourceItemId,
                AnalyzerVersion = "analyze-fake-1",
                Language = "en",
                Normalized = new NormalizedFeatures { Keywords = ["kubernetes"], EventDescriptors = ["release"] },
                Topics = [topic],
                InterestMatches = [new InterestMatch { InterestId = interestId, MatchStrength = matchStrength }],
                Impact = new ImpactSignal { Level = ImpactLevel.None, Confidence = 0.5 },
            });

        public Task<SimilarityResponse> ComputeSimilarityAsync(SimilarityRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new SimilarityResponse
            {
                CorrelationId = request.CorrelationId,
                AlgorithmVersion = "similarity-fake-1",
                Results = request.Representatives
                    .Select(representative => new SimilarityResult
                    {
                        UpdateId = representative.UpdateId,
                        Similarity = SimilarityValue,
                    })
                    .ToList(),
            });

        public Task<GenerateResponse> GenerateAsync(GenerateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Generation is not exercised by grouping tests.");
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        private readonly DateTimeOffset now = new(utcNow, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;
    }
}
