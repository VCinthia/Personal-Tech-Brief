using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Messaging;

namespace PersonalTechBrief.Infrastructure.Messaging;

public sealed class AzureServiceBusContentProcessingMessageReceiver : IContentProcessingMessageReceiver, IAsyncDisposable
{
    private const string TraceParentPropertyName = "traceparent";
    private readonly ServiceBusReceiver receiver;
    private readonly ServiceBusOptions options;
    private readonly ILogger<AzureServiceBusContentProcessingMessageReceiver> logger;

    public AzureServiceBusContentProcessingMessageReceiver(
        ServiceBusClient client,
        IOptions<ServiceBusOptions> options,
        ILogger<AzureServiceBusContentProcessingMessageReceiver> logger)
    {
        this.options = options.Value;
        this.logger = logger;
        receiver = client.CreateReceiver(
            SourceItemReadyEnvelope.Destination,
            new ServiceBusReceiverOptions
            {
                PrefetchCount = this.options.PrefetchCount,
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
            });
    }

    public async Task ReceiveAndDispatchOnceAsync(
        Func<ContentProcessingReceivedMessage, CancellationToken, Task<ContentProcessingMessageDisposition>> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var received = await receiver.ReceiveMessageAsync(
            TimeSpan.FromSeconds(options.ReceiveWaitSeconds),
            cancellationToken);
        if (received is null)
        {
            return;
        }

        ContentProcessingMessageDisposition disposition;
        try
        {
            disposition = await handler(
                new ContentProcessingReceivedMessage(
                    received.MessageId,
                    received.Body.ToString(),
                    received.CorrelationId,
                    TryGetTraceParent(received),
                    received.DeliveryCount),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "The content-processing handler failed unexpectedly for broker message {MessageId}; abandoning for broker retry.",
                received.MessageId);
            await receiver.AbandonMessageAsync(received, cancellationToken: cancellationToken);
            return;
        }

        switch (disposition.Kind)
        {
            case ContentProcessingMessageDispositionKind.Complete:
                await receiver.CompleteMessageAsync(received, cancellationToken);
                break;
            case ContentProcessingMessageDispositionKind.Abandon:
                await receiver.AbandonMessageAsync(received, cancellationToken: cancellationToken);
                break;
            case ContentProcessingMessageDispositionKind.DeadLetter:
                await receiver.DeadLetterMessageAsync(
                    received,
                    disposition.DeadLetterReason,
                    disposition.DeadLetterDescription,
                    cancellationToken);
                break;
            default:
                throw new InvalidOperationException("Content-processing handler returned an unknown disposition.");
        }
    }

    public ValueTask DisposeAsync() => receiver.DisposeAsync();

    private static string? TryGetTraceParent(ServiceBusReceivedMessage received) =>
        received.ApplicationProperties.TryGetValue(TraceParentPropertyName, out var value) && value is string traceParent
            ? traceParent
            : null;
}
