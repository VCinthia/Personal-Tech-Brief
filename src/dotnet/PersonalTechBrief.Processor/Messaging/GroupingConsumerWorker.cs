using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Analysis;
using PersonalTechBrief.Application.Messaging;

namespace PersonalTechBrief.Processor.Messaging;

/// <summary>
/// Drains pending content-processing inbox receipts (Slice 3 durable handoff) in bounded batches and
/// runs the grouping/relevance pipeline for each (pipeline §13 stages 6–8). The pipeline is idempotent
/// and settles each receipt itself: a completed item marks its receipt processed, while a transient
/// failure leaves the receipt pending so the next poll retries it within the bounded budget.
/// </summary>
public sealed class GroupingConsumerWorker(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<GroupingPipelineOptions> options,
    ILogger<GroupingConsumerWorker> logger) : BackgroundService
{
    private readonly GroupingPipelineOptions options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "The grouping/relevance consumer is running with batch size {BatchSize} and poll interval {PollIntervalSeconds}s.",
            options.BatchSize,
            options.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Grouping consumer poll failed; pending receipts remain durable for a bounded retry.");
            }

            await Task.Delay(TimeSpan.FromSeconds(options.PollIntervalSeconds), stoppingToken);
        }
    }

    private async Task DrainOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<PendingContentProcessingReceipt> pending;
        await using (var loadScope = serviceScopeFactory.CreateAsyncScope())
        {
            var inboxStore = loadScope.ServiceProvider.GetRequiredService<IContentProcessingInboxStore>();
            pending = await inboxStore.LoadPendingAsync(options.BatchSize, cancellationToken);
        }

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var receipt in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A fresh scope (and DbContext) per receipt isolates each item's change tracking, so a
            // mid-commit failure on one receipt cannot leak staged entities into another's transaction.
            await using var itemScope = serviceScopeFactory.CreateAsyncScope();
            var pipeline = itemScope.ServiceProvider.GetRequiredService<IGroupingPipelineService>();
            var outcome = await pipeline.ProcessAsync(receipt.Envelope, cancellationToken);
            logger.LogDebug(
                "Grouping pipeline outcome {Outcome} for source item {SourceItemId}.",
                outcome,
                receipt.Envelope.SourceItemId);
        }
    }
}
