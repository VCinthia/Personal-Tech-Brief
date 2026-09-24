using PersonalTechBrief.Application.Messaging;

namespace PersonalTechBrief.UnitTests.Messaging;

public sealed class OutboxDispatcherTests
{
    [Fact]
    public async Task Successful_publish_uses_stable_envelope_and_marks_only_after_send()
    {
        var now = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
        var message = CreatePendingMessage(now);
        var store = new FakeOutboxStore(message);
        var publisher = new RecordingPublisher(actions: store.Actions);
        var dispatcher = new OutboxDispatcher(store, publisher, new FixedTimeProvider(now));

        var result = await dispatcher.DispatchPendingAsync(1, TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.Equal(1, result.DispatchedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.InvalidCount);
        var published = Assert.Single(publisher.Published);
        Assert.Equal(message.Id, published.MessageId);
        Assert.Equal(message.SourceItemId, published.SourceItemId);
        Assert.Equal(message.CorrelationId, published.CorrelationId);
        Assert.Equal(new[] { "publish", "mark" }, store.Actions);
        Assert.Equal(message.Id, Assert.Single(store.Marked));
        Assert.Empty(store.Failures);
    }

    [Fact]
    public async Task Broker_failure_preserves_pending_record_with_safe_diagnostic()
    {
        var now = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
        var message = CreatePendingMessage(now);
        var store = new FakeOutboxStore(message);
        var publisher = new RecordingPublisher(new InvalidOperationException("sensitive connection details"));
        var dispatcher = new OutboxDispatcher(store, publisher, new FixedTimeProvider(now));

        var result = await dispatcher.DispatchPendingAsync(1, TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.Equal(0, result.DispatchedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Empty(store.Marked);
        var failure = Assert.Single(store.Failures);
        Assert.Equal(message.Id, failure.Id);
        Assert.Equal("Broker publish failed: InvalidOperationException.", failure.Diagnostic);
        Assert.DoesNotContain("sensitive", failure.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_outbox_payload_is_not_sent_and_retains_diagnostics()
    {
        var now = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
        var message = CreatePendingMessage(now) with { Payload = "{not-json" };
        var store = new FakeOutboxStore(message);
        var publisher = new RecordingPublisher();
        var dispatcher = new OutboxDispatcher(store, publisher, new FixedTimeProvider(now));

        var result = await dispatcher.DispatchPendingAsync(1, TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.Equal(0, result.DispatchedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(1, result.InvalidCount);
        Assert.Empty(publisher.Published);
        Assert.Empty(store.Failures);
        Assert.Contains("not valid", Assert.Single(store.Quarantines).Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private static PendingOutboxMessage CreatePendingMessage(DateTime occurredAtUtc)
    {
        var messageId = Guid.NewGuid();
        var sourceItemId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var envelope = SourceItemReadyEnvelope.Create(
            messageId,
            sourceItemId,
            sourceId,
            runId,
            correlationId,
            occurredAtUtc,
            "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01");
        return new PendingOutboxMessage(
            messageId,
            sourceItemId,
            sourceId,
            runId,
            correlationId,
            SourceItemReadyEnvelope.Destination,
            envelope.ToJson());
    }

    private sealed class RecordingPublisher : IContentProcessingPublisher
    {
        private readonly Exception? failure;
        private readonly IList<string>? actions;

        public RecordingPublisher(Exception? failure = null, IList<string>? actions = null)
        {
            this.failure = failure;
            this.actions = actions;
        }

        public List<SourceItemReadyEnvelope> Published { get; } = [];

        public Task PublishAsync(SourceItemReadyEnvelope envelope, CancellationToken cancellationToken)
        {
            if (failure is not null)
            {
                throw failure;
            }

            Published.Add(envelope);
            actions?.Add("publish");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOutboxStore(PendingOutboxMessage message) : IOutboxMessageStore
    {
        private PendingOutboxMessage? message = message;

        public List<string> Actions { get; } = [];

        public List<Guid> Marked { get; } = [];

        public List<(Guid Id, string Diagnostic)> Failures { get; } = [];

        public List<(Guid Id, string Diagnostic)> Quarantines { get; } = [];

        public Task<PendingOutboxMessage?> TryClaimNextPendingAsync(
            DateTime retryEligibleBeforeUtc,
            DateTime claimedAtUtc,
            CancellationToken cancellationToken)
        {
            var result = message;
            message = null;
            return Task.FromResult(result);
        }

        public Task MarkDispatchedAsync(Guid outboxMessageId, DateTime dispatchedAtUtc, CancellationToken cancellationToken)
        {
            Actions.Add("mark");
            Marked.Add(outboxMessageId);
            return Task.CompletedTask;
        }

        public Task RecordDispatchFailureAsync(
            Guid outboxMessageId,
            string diagnostic,
            DateTime attemptedAtUtc,
            CancellationToken cancellationToken)
        {
            Failures.Add((outboxMessageId, diagnostic));
            return Task.CompletedTask;
        }

        public Task QuarantineAsync(
            Guid outboxMessageId,
            string diagnostic,
            DateTime attemptedAtUtc,
            CancellationToken cancellationToken)
        {
            Quarantines.Add((outboxMessageId, diagnostic));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
