using Microsoft.Extensions.DependencyInjection;
using PersonalTechBrief.Application.Messaging;

namespace PersonalTechBrief.Processor.Messaging;

public sealed class SourceItemReadyProcessorWorker(
    IContentProcessingMessageReceiver receiver,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<SourceItemReadyProcessorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("The content-processing receiver is running for the Slice 3 idempotent handoff boundary.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await receiver.ReceiveAndDispatchOnceAsync(DispatchAsync, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Content-processing receive failed; retrying after a bounded delay.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<ContentProcessingMessageDisposition> DispatchAsync(
        ContentProcessingReceivedMessage message,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<SourceItemReadyMessageHandler>();
        return await handler.HandleAsync(message, cancellationToken);
    }
}
