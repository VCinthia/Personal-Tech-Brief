using PersonalTechBrief.Application.Briefs;

namespace PersonalTechBrief.Web.Briefs;

/// <summary>
/// Drains the <see cref="IBriefGenerationQueue"/> and runs brief generation asynchronously (the async
/// 202 flow, FR-011/018). Each brief is generated in its own DI scope so scoped persistence is not
/// shared across runs; a failure in one generation never stops the loop.
/// </summary>
public sealed class BriefGenerationBackgroundService(
    IBriefGenerationQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<BriefGenerationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ReconcileOrphanedBriefsAsync(stoppingToken);

        await foreach (var briefId in queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var generator = scope.ServiceProvider.GetRequiredService<IBriefGenerationService>();
                await generator.GenerateAsync(briefId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unhandled error generating brief {BriefId}.", briefId);
            }
        }
    }

    /// <summary>
    /// Fails briefs left in Generating by a prior run before generation resumes, so a crashed/orphaned
    /// brief can never permanently shadow the last completed brief on <c>/current</c>.
    /// </summary>
    private async Task ReconcileOrphanedBriefsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBriefRepository>();
            // Cutoff = now, so only briefs that already existed sweep to Failed; a POST that races
            // startup keeps its brand-new Generating brief (GeneratedAtUtc >= cutoff) for the drain loop.
            var cutoffUtc = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var reconciled = await repository.FailOrphanedGeneratingBriefsAsync(cutoffUtc, cancellationToken);
            if (reconciled > 0)
            {
                logger.LogWarning("Reconciled {Count} orphaned generating brief(s) to failed at startup.", reconciled);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down before reconciliation ran; a later startup will reconcile.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to reconcile orphaned generating briefs at startup.");
        }
    }
}
