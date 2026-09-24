using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Messaging;
using PersonalTechBrief.Infrastructure.Messaging;
using PersonalTechBrief.Processor.Messaging;

namespace PersonalTechBrief.IntegrationTests.Messaging;

// Slice 3: FR-004/006, AC-004, NFR-004/005 and ADR-004/008. These tests exercise
// real AMQP transport and settlements; only the processor's durable inbox is controlled.
public sealed class ServiceBusMessagingTests(ServiceBusEmulatorFixture fixture)
    : IClassFixture<ServiceBusEmulatorFixture>, IAsyncLifetime
{
    private static readonly DateTime ReceivedAtUtc = new(2026, 9, 13, 12, 0, 5, DateTimeKind.Utc);

    private static readonly IOptions<ServiceBusOptions> Options =
        Microsoft.Extensions.Options.Options.Create(new ServiceBusOptions
        {
            OperationTimeoutSeconds = 15,
            ReceiveWaitSeconds = 2,
            PrefetchCount = 0,
        });

    public Task InitializeAsync() => fixture.PurgeQueuesAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Repeated_publication_preserves_identity_and_correlation_and_completes_both_deliveries()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var envelope = CreateEnvelope();
        var inbox = new ControlledInboxStore(ContentProcessingInboxAcceptance.AlreadyAccepted);
        var handler = CreateHandler(inbox);
        await using var client = fixture.CreateClient();
        await using var publisher = new AzureServiceBusContentProcessingPublisher(client, Options);
        await using var receiver = CreateReceiver(client);

        // Replaying an outbox send must keep the same business/message identity.
        // Broker duplicate detection is disabled, so both deliveries reach the consumer;
        // the durable inbox reuses the receipt so both are completed idempotently.
        await publisher.PublishAsync(envelope, timeout.Token);
        await publisher.PublishAsync(envelope, timeout.Token);
        var first = await ReceiveOneAsync(receiver, handler.HandleAsync, timeout.Token);
        var repeated = await ReceiveOneAsync(receiver, handler.HandleAsync, timeout.Token);

        AssertEnvelope(envelope, first);
        AssertEnvelope(envelope, repeated);
        Assert.Equal(1, first.DeliveryCount);
        Assert.Equal(1, repeated.DeliveryCount);
        Assert.Equal(new[] { envelope.SourceItemId, envelope.SourceItemId }, inbox.AcceptedSourceItemIds);
        await AssertQueueEmptyAsync(client, timeout.Token);
        await AssertQueueEmptyAsync(client, timeout.Token, SubQueue.DeadLetter);
    }

    [Fact]
    public async Task Transient_inbox_failure_abandons_then_redelivers_with_incremented_delivery_count()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var envelope = CreateEnvelope();
        var inbox = new ControlledInboxStore(ContentProcessingInboxAcceptance.Accepted)
        {
            FailNextAccept = true,
        };
        var handler = CreateHandler(inbox);
        await using var client = fixture.CreateClient();
        await using var publisher = new AzureServiceBusContentProcessingPublisher(client, Options);
        await using var receiver = CreateReceiver(client);

        await publisher.PublishAsync(envelope, timeout.Token);
        var first = await ReceiveOneAsync(receiver, handler.HandleAsync, timeout.Token);
        var redelivered = await ReceiveOneAsync(receiver, handler.HandleAsync, timeout.Token);

        AssertEnvelope(envelope, first);
        AssertEnvelope(envelope, redelivered);
        Assert.Equal(1, first.DeliveryCount);
        Assert.Equal(first.DeliveryCount + 1, redelivered.DeliveryCount);
        Assert.Equal(new[] { envelope.SourceItemId, envelope.SourceItemId }, inbox.AcceptedSourceItemIds);
        await AssertQueueEmptyAsync(client, timeout.Token);
        await AssertQueueEmptyAsync(client, timeout.Token, SubQueue.DeadLetter);
    }

    [Fact]
    public async Task Unexpected_handler_exception_abandons_message_for_broker_redelivery()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var envelope = CreateEnvelope();
        var handler = CreateHandler(new ControlledInboxStore(ContentProcessingInboxAcceptance.Accepted));
        await using var client = fixture.CreateClient();
        await using var publisher = new AzureServiceBusContentProcessingPublisher(client, Options);
        await using var receiver = CreateReceiver(client);

        await publisher.PublishAsync(envelope, timeout.Token);
        var first = await ReceiveOneAsync(
            receiver,
            (_, _) => throw new InvalidOperationException("Synthetic handler failure."),
            timeout.Token);
        var redelivered = await ReceiveOneAsync(receiver, handler.HandleAsync, timeout.Token);

        AssertEnvelope(envelope, first);
        AssertEnvelope(envelope, redelivered);
        Assert.Equal(1, first.DeliveryCount);
        Assert.Equal(first.DeliveryCount + 1, redelivered.DeliveryCount);
        await AssertQueueEmptyAsync(client, timeout.Token);
    }

    [Fact]
    public async Task Malformed_message_is_moved_to_dead_letter_queue_with_diagnostic_reason()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        const string malformedBody = "{ invalid SourceItemReady";
        var messageId = Guid.NewGuid().ToString("D");
        var correlationId = Guid.NewGuid().ToString("D");
        var inbox = new ControlledInboxStore(ContentProcessingInboxAcceptance.Accepted);
        var handler = CreateHandler(inbox);
        await using var client = fixture.CreateClient();
        await using var sender = client.CreateSender(SourceItemReadyEnvelope.Destination);
        await using var receiver = CreateReceiver(client);

        await sender.SendMessageAsync(new ServiceBusMessage(malformedBody)
        {
            MessageId = messageId,
            CorrelationId = correlationId,
            ContentType = "application/json",
        }, timeout.Token);
        var received = await ReceiveOneAsync(receiver, handler.HandleAsync, timeout.Token);

        Assert.Equal(messageId, received.MessageId);
        Assert.Empty(inbox.AcceptedSourceItemIds);
        await using var deadLetterReceiver = client.CreateReceiver(
            SourceItemReadyEnvelope.Destination,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var deadLettered = await deadLetterReceiver.ReceiveMessageAsync(TimeSpan.FromSeconds(10), timeout.Token);
        Assert.NotNull(deadLettered);
        Assert.Equal(messageId, deadLettered.MessageId);
        Assert.Equal(correlationId, deadLettered.CorrelationId);
        Assert.Equal(malformedBody, deadLettered.Body.ToString());
        Assert.Equal("SourceItemReadyMalformed", deadLettered.DeadLetterReason);
        Assert.Equal("The message body is not valid SourceItemReady JSON.", deadLettered.DeadLetterErrorDescription);
        await deadLetterReceiver.CompleteMessageAsync(deadLettered, timeout.Token);
        await AssertQueueEmptyAsync(client, timeout.Token);
        await AssertQueueEmptyAsync(client, timeout.Token, SubQueue.DeadLetter);
    }

    private static SourceItemReadyMessageHandler CreateHandler(ControlledInboxStore inbox) =>
        new(inbox, new StubTimeProvider(ReceivedAtUtc), NullLogger<SourceItemReadyMessageHandler>.Instance);

    private static AzureServiceBusContentProcessingMessageReceiver CreateReceiver(ServiceBusClient client) =>
        new(client, Options, NullLogger<AzureServiceBusContentProcessingMessageReceiver>.Instance);

    private static async Task<ContentProcessingReceivedMessage> ReceiveOneAsync(
        AzureServiceBusContentProcessingMessageReceiver receiver,
        Func<ContentProcessingReceivedMessage, CancellationToken, Task<ContentProcessingMessageDisposition>> handler,
        CancellationToken cancellationToken)
    {
        ContentProcessingReceivedMessage? received = null;
        while (received is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await receiver.ReceiveAndDispatchOnceAsync((message, token) =>
            {
                received = message;
                return handler(message, token);
            }, cancellationToken);
        }

        return received;
    }

    private static async Task AssertQueueEmptyAsync(
        ServiceBusClient client,
        CancellationToken cancellationToken,
        SubQueue subQueue = SubQueue.None)
    {
        await using var receiver = client.CreateReceiver(
            SourceItemReadyEnvelope.Destination,
            new ServiceBusReceiverOptions { SubQueue = subQueue });
        Assert.Null(await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(1), cancellationToken));
    }

    private static void AssertEnvelope(SourceItemReadyEnvelope expected, ContentProcessingReceivedMessage actual)
    {
        Assert.Equal(expected.MessageId.ToString("D"), actual.MessageId);
        Assert.Equal(expected.CorrelationId.ToString("D"), actual.CorrelationId);
        Assert.Equal(expected.TraceParent, actual.TraceParent);
        Assert.Equal(expected.ToJson(), actual.Body);
    }

    private static SourceItemReadyEnvelope CreateEnvelope() => SourceItemReadyEnvelope.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc),
        "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01");

    private sealed class StubTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private sealed class ControlledInboxStore(ContentProcessingInboxAcceptance acceptance)
        : IContentProcessingInboxStore
    {
        public bool FailNextAccept { get; init; }

        public List<Guid> AcceptedSourceItemIds { get; } = [];

        public Task<ContentProcessingInboxAcceptance> AcceptAsync(
            SourceItemReadyEnvelope envelope,
            DateTime receivedAtUtc,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AcceptedSourceItemIds.Add(envelope.SourceItemId);
            if (FailNextAccept && AcceptedSourceItemIds.Count == 1)
            {
                throw new InvalidOperationException("Synthetic transient database failure.");
            }

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
