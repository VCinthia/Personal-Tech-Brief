using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PersonalTechBrief.Application.Ingestion;
using PersonalTechBrief.Application.Sources;
using PersonalTechBrief.Ingestion.Feeds;

namespace PersonalTechBrief.Ingestion;

/// <summary>
/// Executes one finite ingestion pass. Sources are isolated: a retrieval or parsing failure for
/// one enabled source records a failed run and never prevents later enabled sources from running.
/// Broker dispatch is deliberately outside this job; new items are handed off through the frozen
/// transactional outbox persistence service.
/// </summary>
public sealed class IngestionJob(
    ISourceRepository sourceRepository,
    IIngestionRunService ingestionRunService,
    ISourceItemPersistenceService sourceItemPersistenceService,
    IFeedRetrievalClient feedRetrievalClient,
    TimeProvider timeProvider,
    ILogger<IngestionJob> logger)
{
    public async Task<IngestionJobResult> RunOnceAsync(CancellationToken cancellationToken)
    {
        var sources = await sourceRepository.ListEnabledAsync(cancellationToken);
        var succeededSourceCount = 0;
        var failedSourceCount = 0;
        var newItemCount = 0;

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = await ProcessSourceAsync(source.Id, source, cancellationToken);
            succeededSourceCount += outcome.Succeeded ? 1 : 0;
            failedSourceCount += outcome.Succeeded ? 0 : 1;
            newItemCount += outcome.NewItemCount;
        }

        logger.LogInformation(
            "Completed finite ingestion pass. EnabledSourceCount {EnabledSourceCount}, SucceededSourceCount {SucceededSourceCount}, FailedSourceCount {FailedSourceCount}, NewItemCount {NewItemCount}",
            sources.Count,
            succeededSourceCount,
            failedSourceCount,
            newItemCount);
        return new IngestionJobResult(sources.Count, succeededSourceCount, failedSourceCount, newItemCount);
    }

    private async Task<SourceIngestionOutcome> ProcessSourceAsync(
        Guid sourceId,
        Domain.Sources.Source source,
        CancellationToken cancellationToken)
    {
        var startedAtUtc = GetUtcNow();
        var correlationId = Guid.NewGuid();
        StartedIngestionRun startedRun;
        try
        {
            startedRun = await ingestionRunService.StartAsync(
                new StartIngestionRunCommand(sourceId, correlationId, startedAtUtc),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogSourceFailure(sourceId, null, correlationId, "start_run_failed", exception);
            return SourceIngestionOutcome.Failed;
        }

        var retrievedItemCount = 0;
        var newItemCount = 0;
        try
        {
            var result = await feedRetrievalClient.RetrieveAsync(source, cancellationToken);
            retrievedItemCount = result.IsNotModified ? 0 : result.Items.Count;
            var failedItemCount = 0;
            if (!result.IsNotModified)
            {
                foreach (var item in result.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var persisted = await sourceItemPersistenceService.PersistIfNewAsync(
                            new PersistSourceItemCommand(
                                sourceId,
                                startedRun.IngestionRunId,
                                correlationId,
                                item.ExternalId,
                                item.SourceUrl,
                                item.Title,
                                item.Excerpt,
                                item.PublishedAtUtc,
                                item.ContentHash,
                                GetUtcNow(),
                                Activity.Current?.Id),
                            cancellationToken);
                        newItemCount += persisted.IsDuplicate ? 0 : 1;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        failedItemCount++;
                        LogSourceFailure(
                            sourceId,
                            startedRun.IngestionRunId,
                            correlationId,
                            exception is ArgumentException ? "invalid_feed_item" : "item_persistence_failed",
                            exception);
                    }
                }
            }

            if (failedItemCount > 0)
            {
                // Preserve old validators so a later cycle can retrieve and retry failed
                // entries. Committed entries remain safe through deterministic deduplication.
                await RecordFailureAsync(
                    startedRun,
                    "feed_items_failed",
                    $"{failedItemCount} feed item(s) could not be persisted.",
                    retrievedItemCount,
                    newItemCount,
                    cancellationToken);
                return new SourceIngestionOutcome(false, newItemCount);
            }

            await ingestionRunService.CompleteAsync(
                new CompleteIngestionRunCommand(
                    startedRun.IngestionRunId,
                    result.IsNotModified ? 0 : result.Items.Count,
                    newItemCount,
                    GetUtcNow(),
                    result.IsNotModified,
                    result.ETag,
                    result.LastModified),
                cancellationToken);
            logger.LogInformation(
                "Completed source ingestion. SourceId {SourceId}, IngestionRunId {IngestionRunId}, CorrelationId {CorrelationId}, RetrievedItemCount {RetrievedItemCount}, NewItemCount {NewItemCount}, NotModified {NotModified}",
                sourceId,
                startedRun.IngestionRunId,
                correlationId,
                result.IsNotModified ? 0 : result.Items.Count,
                newItemCount,
                result.IsNotModified);
            return new SourceIngestionOutcome(true, newItemCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FeedRetrievalException exception)
        {
            await RecordFailureAsync(
                startedRun, exception.ErrorCode, exception.SafeDetail, retrievedItemCount, newItemCount, cancellationToken);
            return new SourceIngestionOutcome(false, newItemCount);
        }
        catch (Exception exception)
        {
            await RecordFailureAsync(
                startedRun,
                "ingestion_failed",
                "The source ingestion did not complete successfully.",
                retrievedItemCount,
                newItemCount,
                cancellationToken,
                exception);
            return new SourceIngestionOutcome(false, newItemCount);
        }
    }

    private async Task RecordFailureAsync(
        StartedIngestionRun startedRun,
        string errorCode,
        string safeDetail,
        int retrievedItemCount,
        int newItemCount,
        CancellationToken cancellationToken,
        Exception? exception = null)
    {
        // Failure recording is the last-resort finalizer and must never itself fail on best-effort
        // diagnostics, or the run is left un-finalized. Clamp the counts to the domain invariant
        // (0 <= new <= retrieved) and never let a clock regression push completion before the run
        // start — both are conditions the domain legitimately rejects on the success path.
        var boundedRetrieved = Math.Max(0, retrievedItemCount);
        var boundedNew = Math.Clamp(newItemCount, 0, boundedRetrieved);
        var completedAtUtc = GetUtcNow();
        if (completedAtUtc < startedRun.StartedAtUtc)
        {
            completedAtUtc = startedRun.StartedAtUtc;
        }

        try
        {
            await ingestionRunService.FailAsync(
                new FailIngestionRunCommand(
                    startedRun.IngestionRunId, errorCode, safeDetail, completedAtUtc, boundedRetrieved, boundedNew),
                cancellationToken);
            LogSourceFailure(startedRun.SourceId, startedRun.IngestionRunId, startedRun.CorrelationId, errorCode, exception);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failureRecordingException)
        {
            LogSourceFailure(
                startedRun.SourceId, startedRun.IngestionRunId, startedRun.CorrelationId, "failure_recording_failed", failureRecordingException);
        }
    }

    private DateTime GetUtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private void LogSourceFailure(Guid sourceId, Guid? ingestionRunId, Guid correlationId, string errorCode, Exception? exception) =>
        logger.LogWarning(
            "Source ingestion failed. SourceId {SourceId}, IngestionRunId {IngestionRunId}, CorrelationId {CorrelationId}, ErrorCode {ErrorCode}, ExceptionType {ExceptionType}",
            sourceId,
            ingestionRunId,
            correlationId,
            errorCode,
            exception?.GetType().Name);

    private sealed record SourceIngestionOutcome(bool Succeeded, int NewItemCount)
    {
        public static SourceIngestionOutcome Failed { get; } = new(false, 0);
    }
}

public sealed record IngestionJobResult(
    int EnabledSourceCount,
    int SucceededSourceCount,
    int FailedSourceCount,
    int NewItemCount)
{
    public int ExitCode => EnabledSourceCount > 0 && SucceededSourceCount == 0 ? 1 : 0;
}
