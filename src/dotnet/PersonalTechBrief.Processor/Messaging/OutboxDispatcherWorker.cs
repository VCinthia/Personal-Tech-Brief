using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Messaging;

namespace PersonalTechBrief.Processor.Messaging;

public sealed class OutboxDispatcherWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<OutboxDispatchOptions> options,
    ILogger<OutboxDispatcherWorker> logger) : BackgroundService
{
    private readonly OutboxDispatchOptions dispatchOptions = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "The transactional-outbox dispatcher is running with batch size {BatchSize} and lease duration {LeaseDurationSeconds} seconds.",
            dispatchOptions.BatchSize,
            dispatchOptions.LeaseDurationSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
                var result = await dispatcher.DispatchPendingAsync(
                    dispatchOptions.BatchSize,
                    TimeSpan.FromSeconds(dispatchOptions.LeaseDurationSeconds),
                    stoppingToken);
                if (result.HasActivity)
                {
                    logger.LogInformation(
                        "Outbox dispatch batch completed: {DispatchedCount} dispatched, {FailedCount} failed, {InvalidCount} invalid.",
                        result.DispatchedCount,
                        result.FailedCount,
                        result.InvalidCount);
                }

                if (result.InvalidCount > 0)
                {
                    logger.LogWarning(
                        "Quarantined {InvalidCount} invalid outbox records; bounded diagnostics remain in SQL for operational repair.",
                        result.InvalidCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Transactional-outbox dispatch failed; pending records remain durable for a bounded retry.");
            }

            await Task.Delay(TimeSpan.FromSeconds(dispatchOptions.PollIntervalSeconds), stoppingToken);
        }
    }
}
