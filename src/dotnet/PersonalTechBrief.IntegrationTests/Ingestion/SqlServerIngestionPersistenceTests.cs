using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTechBrief.Application.Ingestion;
using PersonalTechBrief.Application.Messaging;
using PersonalTechBrief.Application.Sources;
using PersonalTechBrief.Domain.Messaging;
using PersonalTechBrief.Infrastructure.Messaging;
using PersonalTechBrief.Domain.Sources;
using PersonalTechBrief.Infrastructure.Persistence;
using PersonalTechBrief.IntegrationTests.Interests;

namespace PersonalTechBrief.IntegrationTests.Ingestion;

public sealed class SqlServerIngestionPersistenceTests(SqlServerInterestApiFixture fixture)
    : IClassFixture<SqlServerInterestApiFixture>
{
    [Fact]
    public async Task Committed_sql_server_migrations_atomically_persist_new_item_and_one_stable_outbox_envelope()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        await using var operationScope = factory.Services.CreateAsyncScope();
        var (source, run) = await CreateSourceAndRunAsync(operationScope.ServiceProvider);
        var persistence = operationScope.ServiceProvider.GetRequiredService<ISourceItemPersistenceService>();
        var retrievedAtUtc = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
        var correlationId = Guid.NewGuid();
        var command = new PersistSourceItemCommand(
            source.Id,
            run.IngestionRunId,
            correlationId,
            "article-42",
            "https://news.example.test/Article/42?source=rss",
            "A durable handoff",
            "Fixture excerpt",
            retrievedAtUtc.AddMinutes(-2),
            "content-hash-42",
            retrievedAtUtc,
            "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01");

        var first = await persistence.PersistIfNewAsync(command, CancellationToken.None);
        var duplicate = await persistence.PersistIfNewAsync(
            command with { SourceUrl = "https://news.example.test/a-different-url", Title = "Changed title" },
            CancellationToken.None);

        Assert.False(first.IsDuplicate);
        Assert.NotNull(first.SourceItemId);
        Assert.NotNull(first.OutboxMessageId);
        Assert.True(duplicate.IsDuplicate);
        Assert.Null(duplicate.SourceItemId);
        Assert.Null(duplicate.OutboxMessageId);

        var runService = operationScope.ServiceProvider.GetRequiredService<IIngestionRunService>();
        await runService.CompleteAsync(
            new CompleteIngestionRunCommand(
                run.IngestionRunId,
                RetrievedItemCount: 2,
                NewItemCount: 1,
                CompletedAtUtc: retrievedAtUtc.AddMinutes(1),
                NotModified: false,
                ETag: "fixture-etag",
                LastModified: new DateTimeOffset(retrievedAtUtc.AddMinutes(-1))),
            CancellationToken.None);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        Assert.Equal(1, await dbContext.SourceItems.CountAsync());
        var persistedRun = await dbContext.IngestionRuns.SingleAsync();
        Assert.Equal("Succeeded", persistedRun.Status.ToString());
        Assert.Equal(2, persistedRun.RetrievedItemCount);
        Assert.Equal(1, persistedRun.NewItemCount);
        var persistedSource = await dbContext.Sources.SingleAsync();
        Assert.Equal("Succeeded", persistedSource.LastIngestionStatus);
        Assert.Equal("fixture-etag", persistedSource.ETag);
        var outbox = await dbContext.OutboxMessages.SingleAsync();
        Assert.Equal(first.OutboxMessageId, outbox.Id);
        Assert.Equal(first.SourceItemId, outbox.SourceItemId);
        Assert.Equal("Pending", outbox.Status.ToString());
        var envelope = JsonSerializer.Deserialize<SourceItemReadyEnvelope>(outbox.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(envelope);
        Assert.Equal(outbox.Id, envelope.MessageId);
        Assert.Equal(outbox.SourceItemId, envelope.SourceItemId);
        Assert.Equal(source.Id, envelope.SourceId);
        Assert.Equal(run.IngestionRunId, envelope.IngestionRunId);
        Assert.Equal(correlationId, envelope.CorrelationId);
        Assert.Equal(retrievedAtUtc, envelope.OccurredAtUtc);

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        Assert.Contains(
            appliedMigrations,
            migration => migration.EndsWith("AddIngestionAndTransactionalOutbox", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Title_window_fallback_deduplicates_only_when_stronger_keys_are_absent()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        await using var operationScope = factory.Services.CreateAsyncScope();
        var (source, run) = await CreateSourceAndRunAsync(operationScope.ServiceProvider);
        var persistence = operationScope.ServiceProvider.GetRequiredService<ISourceItemPersistenceService>();
        var now = new DateTime(2026, 9, 13, 13, 0, 0, DateTimeKind.Utc);

        var first = await persistence.PersistIfNewAsync(
            new PersistSourceItemCommand(
                source.Id, run.IngestionRunId, Guid.NewGuid(), null, null, "Same event", null, null, null, now, null),
            CancellationToken.None);
        var inWindow = await persistence.PersistIfNewAsync(
            new PersistSourceItemCommand(
                source.Id, run.IngestionRunId, Guid.NewGuid(), null, null, " same   EVENT ", null, null, null, now.AddDays(6), null),
            CancellationToken.None);
        var outsideWindow = await persistence.PersistIfNewAsync(
            new PersistSourceItemCommand(
                source.Id, run.IngestionRunId, Guid.NewGuid(), null, null, "same event", null, null, null, now.AddDays(8), null),
            CancellationToken.None);

        Assert.False(first.IsDuplicate);
        Assert.True(inWindow.IsDuplicate);
        Assert.False(outsideWindow.IsDuplicate);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        Assert.Equal(2, await dbContext.SourceItems.CountAsync());
        Assert.Equal(2, await dbContext.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Sql_outbox_claim_prevents_parallel_dispatchers_from_sending_one_pending_record_concurrently()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        var now = new DateTime(2026, 9, 13, 14, 0, 0, DateTimeKind.Utc);

        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var (source, run) = await CreateSourceAndRunAsync(setupScope.ServiceProvider);
            var persistence = setupScope.ServiceProvider.GetRequiredService<ISourceItemPersistenceService>();
            var persisted = await persistence.PersistIfNewAsync(
                new PersistSourceItemCommand(
                    source.Id,
                    run.IngestionRunId,
                    Guid.NewGuid(),
                    "outbox-claim-fixture",
                    "https://news.example.test/outbox-claim-fixture",
                    "Outbox claim fixture",
                    null,
                    now,
                    null,
                    now,
                    null),
                CancellationToken.None);
            Assert.False(persisted.IsDuplicate);
        }

        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstStore = new SqlOutboxMessageStore(
            firstScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>());
        var secondStore = new SqlOutboxMessageStore(
            secondScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>());

        var firstClaim = await firstStore.TryClaimNextPendingAsync(
            now.AddSeconds(-30),
            now,
            CancellationToken.None);
        var concurrentClaim = await secondStore.TryClaimNextPendingAsync(
            now.AddSeconds(-30),
            now,
            CancellationToken.None);

        Assert.NotNull(firstClaim);
        Assert.Null(concurrentClaim);

        await firstStore.MarkDispatchedAsync(firstClaim.Id, now.AddSeconds(1), CancellationToken.None);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        var outbox = await dbContext.OutboxMessages.SingleAsync();
        Assert.Equal(OutboxMessageStatus.Dispatched, outbox.Status);
        Assert.Equal(1, outbox.DispatchAttemptCount);
        Assert.Equal(now.AddSeconds(1), outbox.DispatchedAtUtc);
    }

    private static async Task<(Source Source, StartedIngestionRun Run)> CreateSourceAndRunAsync(
        IServiceProvider serviceProvider)
    {
        var sourceRepository = serviceProvider.GetRequiredService<ISourceRepository>();
        var source = Source.Create(
            "Fixture feed",
            "https://feeds.example.test/fixture.xml",
            new DateTime(2026, 9, 13, 11, 0, 0, DateTimeKind.Utc));
        sourceRepository.Add(source);
        await sourceRepository.SaveChangesAsync(CancellationToken.None);

        var runService = serviceProvider.GetRequiredService<IIngestionRunService>();
        var run = await runService.StartAsync(
            new StartIngestionRunCommand(source.Id, Guid.NewGuid(), new DateTime(2026, 9, 13, 11, 30, 0, DateTimeKind.Utc)),
            CancellationToken.None);
        return (source, run);
    }
}
