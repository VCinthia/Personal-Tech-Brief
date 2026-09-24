using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTechBrief.Application.Ingestion;
using PersonalTechBrief.Application.Messaging;
using PersonalTechBrief.Application.Sources;
using PersonalTechBrief.Domain.Sources;
using PersonalTechBrief.Infrastructure.Messaging;
using PersonalTechBrief.Infrastructure.Persistence;
using PersonalTechBrief.IntegrationTests.Interests;

namespace PersonalTechBrief.IntegrationTests.Messaging;

// Slice 3 durable-handoff remediation: the consumer must commit a unique SQL inbox
// receipt before broker completion, reuse it on duplicate delivery, and let a fresh
// process recover pending work from SQL after the broker message is gone.
public sealed class SqlServerContentProcessingInboxTests(SqlServerInterestApiFixture fixture)
    : IClassFixture<SqlServerInterestApiFixture>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Accept_commits_one_receipt_reuses_it_on_replay_and_lets_a_fresh_process_recover_it()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        var receivedAtUtc = new DateTime(2026, 9, 13, 12, 5, 0, DateTimeKind.Utc);
        var envelope = await PersistItemAndReadEnvelopeAsync(factory, "inbox-recover-1");

        // The consumer process accepts the delivery and commits a durable receipt.
        await using (var acceptScope = factory.Services.CreateAsyncScope())
        {
            var inbox = new SqlContentProcessingInboxStore(
                acceptScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>());
            var first = await inbox.AcceptAsync(envelope, receivedAtUtc, CancellationToken.None);
            var replay = await inbox.AcceptAsync(envelope, receivedAtUtc.AddSeconds(30), CancellationToken.None);
            Assert.Equal(ContentProcessingInboxAcceptance.Accepted, first);
            Assert.Equal(ContentProcessingInboxAcceptance.AlreadyAccepted, replay);
        }

        // A fresh process (new scope) recovers the pending receipt from SQL alone.
        await using (var recoveryScope = factory.Services.CreateAsyncScope())
        {
            var inbox = new SqlContentProcessingInboxStore(
                recoveryScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>());
            var pending = await inbox.LoadPendingAsync(10, CancellationToken.None);
            var recovered = Assert.Single(pending);
            Assert.Equal(envelope.SourceItemId, recovered.Envelope.SourceItemId);
            Assert.Equal(envelope.MessageId, recovered.Envelope.MessageId);
            Assert.Equal(envelope.CorrelationId, recovered.Envelope.CorrelationId);
            Assert.Equal(receivedAtUtc, recovered.ReceivedAtUtc);
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        Assert.Equal(1, await dbContext.ContentProcessingInbox.CountAsync());
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        Assert.Contains(
            appliedMigrations,
            migration => migration.EndsWith("AddContentProcessingInbox", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Accept_rejects_a_missing_item_and_a_reference_mismatch_without_committing_a_receipt()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        var receivedAtUtc = new DateTime(2026, 9, 13, 12, 6, 0, DateTimeKind.Utc);
        var envelope = await PersistItemAndReadEnvelopeAsync(factory, "inbox-reject-1");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var inbox = new SqlContentProcessingInboxStore(
                scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>());

            var missing = await inbox.AcceptAsync(
                SourceItemReadyEnvelope.Create(
                    envelope.MessageId, Guid.NewGuid(), envelope.SourceId, envelope.IngestionRunId,
                    envelope.CorrelationId, envelope.OccurredAtUtc, envelope.TraceParent),
                receivedAtUtc,
                CancellationToken.None);
            Assert.Equal(ContentProcessingInboxAcceptance.SourceItemNotFound, missing);

            var mismatch = await inbox.AcceptAsync(
                SourceItemReadyEnvelope.Create(
                    envelope.MessageId, envelope.SourceItemId, Guid.NewGuid(), envelope.IngestionRunId,
                    envelope.CorrelationId, envelope.OccurredAtUtc, envelope.TraceParent),
                receivedAtUtc,
                CancellationToken.None);
            Assert.Equal(ContentProcessingInboxAcceptance.ReferenceMismatch, mismatch);
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        Assert.Equal(0, await dbContext.ContentProcessingInbox.CountAsync());
    }

    private static async Task<SourceItemReadyEnvelope> PersistItemAndReadEnvelopeAsync(
        SqlServerInterestApiFactory factory,
        string externalId)
    {
        await using var operationScope = factory.Services.CreateAsyncScope();
        var serviceProvider = operationScope.ServiceProvider;

        var sourceRepository = serviceProvider.GetRequiredService<ISourceRepository>();
        var source = Source.Create(
            "Fixture feed",
            "https://feeds.example.test/inbox-fixture.xml",
            new DateTime(2026, 9, 13, 11, 0, 0, DateTimeKind.Utc));
        sourceRepository.Add(source);
        await sourceRepository.SaveChangesAsync(CancellationToken.None);

        var runService = serviceProvider.GetRequiredService<IIngestionRunService>();
        var run = await runService.StartAsync(
            new StartIngestionRunCommand(source.Id, Guid.NewGuid(), new DateTime(2026, 9, 13, 11, 30, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        var persistence = serviceProvider.GetRequiredService<ISourceItemPersistenceService>();
        var retrievedAtUtc = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
        var persisted = await persistence.PersistIfNewAsync(
            new PersistSourceItemCommand(
                source.Id,
                run.IngestionRunId,
                Guid.NewGuid(),
                externalId,
                $"https://news.example.test/{externalId}",
                "A durable handoff",
                "Fixture excerpt",
                retrievedAtUtc.AddMinutes(-2),
                $"hash-{externalId}",
                retrievedAtUtc,
                "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01"),
            CancellationToken.None);
        Assert.False(persisted.IsDuplicate);

        var dbContext = serviceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        var outbox = await dbContext.OutboxMessages.AsNoTracking().SingleAsync();
        var envelope = JsonSerializer.Deserialize<SourceItemReadyEnvelope>(outbox.Payload, SerializerOptions);
        Assert.NotNull(envelope);
        return envelope;
    }
}
