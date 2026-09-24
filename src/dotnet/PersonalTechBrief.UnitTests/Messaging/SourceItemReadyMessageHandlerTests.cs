using Microsoft.Extensions.Logging.Abstractions;
using PersonalTechBrief.Application.Messaging;
using PersonalTechBrief.Processor.Messaging;

namespace PersonalTechBrief.UnitTests.Messaging;

public sealed class SourceItemReadyMessageHandlerTests
{
    private static readonly DateTime ReceivedAtUtc = new(2026, 9, 13, 12, 0, 5, DateTimeKind.Utc);

    [Fact]
    public async Task Accepted_item_commits_an_inbox_receipt_and_is_completed()
    {
        var envelope = CreateEnvelope();
        var inbox = new FakeInboxStore(ContentProcessingInboxAcceptance.Accepted);
        var handler = CreateHandler(inbox);

        var disposition = await handler.HandleAsync(CreateReceivedMessage(envelope), CancellationToken.None);

        Assert.Equal(ContentProcessingMessageDispositionKind.Complete, disposition.Kind);
        Assert.Equal(envelope.SourceItemId, inbox.AcceptedEnvelope?.SourceItemId);
        Assert.Equal(ReceivedAtUtc, inbox.ReceivedAtUtc);
    }

    [Fact]
    public async Task Duplicate_delivery_reuses_the_receipt_and_is_completed()
    {
        var envelope = CreateEnvelope();
        var handler = CreateHandler(new FakeInboxStore(ContentProcessingInboxAcceptance.AlreadyAccepted));

        var disposition = await handler.HandleAsync(CreateReceivedMessage(envelope), CancellationToken.None);

        Assert.Equal(ContentProcessingMessageDispositionKind.Complete, disposition.Kind);
    }

    [Fact]
    public async Task Terminal_item_is_completed_idempotently_without_a_new_receipt()
    {
        var envelope = CreateEnvelope();
        var handler = CreateHandler(new FakeInboxStore(ContentProcessingInboxAcceptance.SourceItemTerminal));

        var disposition = await handler.HandleAsync(CreateReceivedMessage(envelope), CancellationToken.None);

        Assert.Equal(ContentProcessingMessageDispositionKind.Complete, disposition.Kind);
    }

    [Fact]
    public async Task Malformed_body_is_dead_lettered_without_touching_the_inbox()
    {
        var inbox = new FakeInboxStore(ContentProcessingInboxAcceptance.Accepted);
        var handler = CreateHandler(inbox);
        var message = new ContentProcessingReceivedMessage(
            Guid.NewGuid().ToString("D"),
            "{ invalid",
            Guid.NewGuid().ToString("D"),
            null,
            1);

        var disposition = await handler.HandleAsync(message, CancellationToken.None);

        Assert.Equal(ContentProcessingMessageDispositionKind.DeadLetter, disposition.Kind);
        Assert.Equal("SourceItemReadyMalformed", disposition.DeadLetterReason);
        Assert.Null(inbox.AcceptedEnvelope);
    }

    [Fact]
    public async Task Missing_source_item_is_dead_lettered()
    {
        var envelope = CreateEnvelope();
        var handler = CreateHandler(new FakeInboxStore(ContentProcessingInboxAcceptance.SourceItemNotFound));

        var disposition = await handler.HandleAsync(CreateReceivedMessage(envelope), CancellationToken.None);

        Assert.Equal(ContentProcessingMessageDispositionKind.DeadLetter, disposition.Kind);
        Assert.Equal("SourceItemNotFound", disposition.DeadLetterReason);
    }

    [Theory]
    [InlineData(ContentProcessingInboxAcceptance.ReferenceMismatch)]
    [InlineData(ContentProcessingInboxAcceptance.InvalidSourceItemState)]
    public async Task Reference_or_state_mismatch_is_dead_lettered_as_invalid(
        ContentProcessingInboxAcceptance acceptance)
    {
        var envelope = CreateEnvelope();
        var handler = CreateHandler(new FakeInboxStore(acceptance));

        var disposition = await handler.HandleAsync(CreateReceivedMessage(envelope), CancellationToken.None);

        Assert.Equal(ContentProcessingMessageDispositionKind.DeadLetter, disposition.Kind);
        Assert.Equal("SourceItemReferenceInvalid", disposition.DeadLetterReason);
    }

    [Fact]
    public async Task Inbox_failure_is_abandoned_for_broker_bounded_retry()
    {
        var envelope = CreateEnvelope();
        var handler = CreateHandler(new FakeInboxStore(new InvalidOperationException("database unavailable")));

        var disposition = await handler.HandleAsync(CreateReceivedMessage(envelope), CancellationToken.None);

        Assert.Equal(ContentProcessingMessageDispositionKind.Abandon, disposition.Kind);
    }

    [Fact]
    public async Task Noncanonical_broker_id_is_dead_lettered_without_touching_the_inbox()
    {
        var envelope = CreateEnvelope();
        var message = CreateReceivedMessage(envelope) with { MessageId = envelope.MessageId.ToString("N") };
        var inbox = new FakeInboxStore(ContentProcessingInboxAcceptance.Accepted);
        var handler = CreateHandler(inbox);

        var disposition = await handler.HandleAsync(message, CancellationToken.None);

        Assert.Equal(ContentProcessingMessageDispositionKind.DeadLetter, disposition.Kind);
        Assert.Equal("SourceItemReadyMalformed", disposition.DeadLetterReason);
        Assert.Null(inbox.AcceptedEnvelope);
    }

    private static SourceItemReadyMessageHandler CreateHandler(FakeInboxStore inbox) =>
        new(
            inbox,
            new StubTimeProvider(ReceivedAtUtc),
            NullLogger<SourceItemReadyMessageHandler>.Instance);

    private static SourceItemReadyEnvelope CreateEnvelope() =>
        SourceItemReadyEnvelope.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc),
            "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01");

    private static ContentProcessingReceivedMessage CreateReceivedMessage(SourceItemReadyEnvelope envelope) =>
        new(
            envelope.MessageId.ToString("D"),
            envelope.ToJson(),
            envelope.CorrelationId.ToString("D"),
            envelope.TraceParent,
            1);

    private sealed class StubTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private sealed class FakeInboxStore : IContentProcessingInboxStore
    {
        private readonly ContentProcessingInboxAcceptance acceptance;
        private readonly Exception? failure;

        public FakeInboxStore(ContentProcessingInboxAcceptance acceptance)
        {
            this.acceptance = acceptance;
        }

        public FakeInboxStore(Exception failure)
        {
            this.failure = failure;
        }

        public SourceItemReadyEnvelope? AcceptedEnvelope { get; private set; }

        public DateTime? ReceivedAtUtc { get; private set; }

        public Task<ContentProcessingInboxAcceptance> AcceptAsync(
            SourceItemReadyEnvelope envelope,
            DateTime receivedAtUtc,
            CancellationToken cancellationToken)
        {
            if (failure is not null)
            {
                throw failure;
            }

            AcceptedEnvelope = envelope;
            ReceivedAtUtc = receivedAtUtc;
            return Task.FromResult(acceptance);
        }

        public Task<IReadOnlyList<PendingContentProcessingReceipt>> LoadPendingAsync(
            int batchSize,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PendingContentProcessingReceipt>>([]);

        public Task MarkProcessedAsync(Guid sourceItemId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
