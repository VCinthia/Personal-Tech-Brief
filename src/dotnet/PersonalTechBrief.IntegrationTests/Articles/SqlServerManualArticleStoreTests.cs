using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTechBrief.Application.Articles;
using PersonalTechBrief.Application.Ingestion;
using PersonalTechBrief.Application.Messaging;
using PersonalTechBrief.Application.Sources;
using PersonalTechBrief.Domain.Ingestion;
using PersonalTechBrief.Infrastructure.Messaging;
using PersonalTechBrief.Infrastructure.Persistence;
using PersonalTechBrief.Domain.Sources;
using PersonalTechBrief.IntegrationTests.Interests;

namespace PersonalTechBrief.IntegrationTests.Articles;

public sealed class SqlServerManualArticleStoreTests(SqlServerInterestApiFixture fixture)
    : IClassFixture<SqlServerInterestApiFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateTime RetrievedAtUtc = new(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Persists_a_source_less_manual_item_and_outbox_that_the_inbox_accepts()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();

        var correlationId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IManualArticleStore>();
            var result = await store.PersistIfNewAsync(
                new PersistManualArticleCommand(
                    "https://blog.example.test/manual-article",
                    "A manually submitted article",
                    "The extracted body text of the article.",
                    "MANUALHASH01",
                    correlationId,
                    RetrievedAtUtc,
                    null),
                CancellationToken.None);

            Assert.False(result.IsDuplicate);
            Assert.NotNull(result.SourceItemId);
            Assert.NotNull(result.OutboxMessageId);
        }

        SourceItemReadyEnvelope envelope;
        await using (var verifyScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = verifyScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
            var item = await dbContext.SourceItems.SingleAsync();
            Assert.Equal(SourceItemOriginType.ManualUrl, item.OriginType);
            Assert.Null(item.SourceId);
            Assert.Equal(SourceItemProcessingStatus.Queued, item.ProcessingStatus);

            var outbox = await dbContext.OutboxMessages.SingleAsync();
            Assert.Null(outbox.SourceId);
            Assert.Null(outbox.IngestionRunId);
            Assert.Equal(item.Id, outbox.SourceItemId);

            envelope = JsonSerializer.Deserialize<SourceItemReadyEnvelope>(outbox.Payload, SerializerOptions)!;
            Assert.Null(envelope.SourceId);
            Assert.Null(envelope.IngestionRunId);
            Assert.Equal(correlationId, envelope.CorrelationId);
        }

        // The durable inbox accepts the source-less handoff end to end.
        await using (var inboxScope = factory.Services.CreateAsyncScope())
        {
            var inbox = new SqlContentProcessingInboxStore(
                inboxScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>());
            var acceptance = await inbox.AcceptAsync(envelope, RetrievedAtUtc.AddSeconds(5), CancellationToken.None);
            Assert.Equal(ContentProcessingInboxAcceptance.Accepted, acceptance);
        }
    }

    [Fact]
    public async Task Deduplicates_a_resubmitted_url_and_returns_the_existing_identifier()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IManualArticleStore>();
        var command = new PersistManualArticleCommand(
            "https://blog.example.test/dedupe-me",
            "Title",
            "Body.",
            "HASHDEDUPE",
            Guid.NewGuid(),
            RetrievedAtUtc,
            null);

        var first = await store.PersistIfNewAsync(command, CancellationToken.None);
        var second = await store.PersistIfNewAsync(
            command with { CorrelationId = Guid.NewGuid(), ContentHash = "OTHERHASH" },
            CancellationToken.None);

        Assert.False(first.IsDuplicate);
        Assert.True(second.IsDuplicate);
        Assert.Equal(first.SourceItemId, second.SourceItemId);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        Assert.Equal(1, await dbContext.SourceItems.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task A_manual_url_already_ingested_by_a_feed_is_a_duplicate()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        const string sharedUrl = "https://news.example.test/shared/story";

        await using (var feedScope = factory.Services.CreateAsyncScope())
        {
            var (source, run) = await CreateSourceAndRunAsync(feedScope.ServiceProvider);
            var feedPersistence = feedScope.ServiceProvider.GetRequiredService<ISourceItemPersistenceService>();
            var feedResult = await feedPersistence.PersistIfNewAsync(
                new PersistSourceItemCommand(
                    source.Id, run.IngestionRunId, Guid.NewGuid(), "feed-1", sharedUrl,
                    "Feed story", "Feed excerpt", null, null, RetrievedAtUtc, null),
                CancellationToken.None);
            Assert.False(feedResult.IsDuplicate);
        }

        await using var manualScope = factory.Services.CreateAsyncScope();
        var store = manualScope.ServiceProvider.GetRequiredService<IManualArticleStore>();
        var manual = await store.PersistIfNewAsync(
            new PersistManualArticleCommand(sharedUrl, "Manual story", "Manual body.", "MANUALHASH", Guid.NewGuid(), RetrievedAtUtc.AddMinutes(1), null),
            CancellationToken.None);

        Assert.True(manual.IsDuplicate);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        Assert.Equal(1, await dbContext.SourceItems.CountAsync());
    }

    [Fact]
    public async Task Concurrent_submissions_of_the_same_url_persist_exactly_one_item()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        const string url = "https://blog.example.test/concurrent-race";

        // Distinct content hashes so only the normalized-URL key can deduplicate: this exercises
        // the filtered unique index + duplicate catch rather than the pre-insert content-hash SELECT.
        async Task<PersistManualArticleResult> SubmitAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IManualArticleStore>();
            return await store.PersistIfNewAsync(
                new PersistManualArticleCommand(url, "Race", "Body.", "HASH" + Guid.NewGuid().ToString("N"), Guid.NewGuid(), RetrievedAtUtc, null),
                CancellationToken.None);
        }

        var results = await Task.WhenAll(SubmitAsync(), SubmitAsync());

        Assert.Equal(1, results.Count(result => !result.IsDuplicate));

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        Assert.Equal(1, await dbContext.SourceItems.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
    }

    private static async Task<(Source Source, StartedIngestionRun Run)> CreateSourceAndRunAsync(IServiceProvider serviceProvider)
    {
        var sourceRepository = serviceProvider.GetRequiredService<ISourceRepository>();
        var source = Source.Create("Fixture feed", "https://feeds.example.test/fixture.xml", RetrievedAtUtc.AddHours(-1));
        sourceRepository.Add(source);
        await sourceRepository.SaveChangesAsync(CancellationToken.None);

        var runService = serviceProvider.GetRequiredService<IIngestionRunService>();
        var run = await runService.StartAsync(
            new StartIngestionRunCommand(source.Id, Guid.NewGuid(), RetrievedAtUtc.AddMinutes(-30)),
            CancellationToken.None);
        return (source, run);
    }
}
