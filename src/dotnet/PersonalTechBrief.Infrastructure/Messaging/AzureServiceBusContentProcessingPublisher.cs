using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Messaging;

namespace PersonalTechBrief.Infrastructure.Messaging;

public sealed class AzureServiceBusContentProcessingPublisher : IContentProcessingPublisher, IAsyncDisposable
{
    private const string TraceParentPropertyName = "traceparent";
    private readonly ServiceBusSender sender;
    private readonly ServiceBusOptions options;

    public AzureServiceBusContentProcessingPublisher(
        ServiceBusClient client,
        IOptions<ServiceBusOptions> options)
    {
        this.options = options.Value;
        sender = client.CreateSender(SourceItemReadyEnvelope.Destination);
    }

    public async Task PublishAsync(SourceItemReadyEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.OperationTimeoutSeconds));

        var message = new ServiceBusMessage(BinaryData.FromString(envelope.ToJson()))
        {
            MessageId = envelope.MessageId.ToString("D"),
            CorrelationId = envelope.CorrelationId.ToString("D"),
            ContentType = "application/json",
        };
        if (envelope.TraceParent is not null)
        {
            message.ApplicationProperties[TraceParentPropertyName] = envelope.TraceParent;
        }

        await sender.SendMessageAsync(message, timeout.Token);
    }

    public ValueTask DisposeAsync() => sender.DisposeAsync();
}
