using PersonalTechBrief.Domain.Ingestion;
using PersonalTechBrief.Domain.Messaging;
using PersonalTechBrief.Domain.Sources;

namespace PersonalTechBrief.UnitTests.Ingestion;

public sealed class IngestionDomainTests
{
    [Fact]
    public void Feed_item_preserves_source_traceability_and_starts_queued()
    {
        var sourceId = Guid.NewGuid();
        var retrievedAtUtc = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);

        var item = SourceItem.CreateFeed(
            sourceId,
            "entry-7",
            "HTTPS://Example.test/News/Item?Ref=Source#section",
            "  Important\tnews  ",
            "A concise excerpt.",
            retrievedAtUtc.AddMinutes(-5),
            "ab12",
            retrievedAtUtc);

        Assert.Equal(SourceItemOriginType.Feed, item.OriginType);
        Assert.Equal(SourceItemProcessingStatus.Queued, item.ProcessingStatus);
        Assert.Equal("entry-7", item.ExternalId);
        Assert.Equal("HTTPS://Example.test/News/Item?Ref=Source#section", item.OriginalUrl);
        Assert.Equal("https://example.test/News/Item?Ref=Source", item.NormalizedUrl);
        Assert.Equal("Important news", item.Title);
        Assert.Equal("IMPORTANT NEWS", item.NormalizedTitle);
        Assert.Equal("AB12", item.ContentHash);
    }

    [Fact]
    public void Ingestion_run_records_not_modified_as_success_with_no_items()
    {
        var startedAtUtc = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
        var run = IngestionRun.Start(Guid.NewGuid(), Guid.NewGuid(), startedAtUtc);

        run.CompleteSuccessfully(0, 0, startedAtUtc.AddSeconds(1), notModified: true);

        Assert.Equal(IngestionRunStatus.NotModified, run.Status);
        Assert.Equal(0, run.RetrievedItemCount);
        Assert.Equal(0, run.NewItemCount);
        Assert.NotNull(run.CompletedAtUtc);
    }

    [Fact]
    public void Outbox_failure_preserves_pending_status_and_diagnostics()
    {
        var now = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
        var message = OutboxMessage.CreatePending(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "content-processing",
            "{}",
            now);

        message.RecordDispatchFailure("broker unavailable", now.AddSeconds(1));

        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal(1, message.DispatchAttemptCount);
        Assert.Equal("broker unavailable", message.LastDispatchError);
        Assert.Null(message.DispatchedAtUtc);
    }

    [Fact]
    public void Source_records_completed_ingestion_metadata_without_erasing_traceability()
    {
        var startedAtUtc = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
        var source = Source.Create("Fixture", "https://feeds.example.test/feed.xml", startedAtUtc);

        source.RecordIngestionOutcome(
            IngestionRunStatus.Succeeded,
            startedAtUtc.AddMinutes(1),
            "etag-22",
            new DateTimeOffset(startedAtUtc.AddMinutes(-5)));

        Assert.Equal("Succeeded", source.LastIngestionStatus);
        Assert.Equal(startedAtUtc.AddMinutes(1), source.LastIngestionAtUtc);
        Assert.Equal("etag-22", source.ETag);
        Assert.Equal("https://feeds.example.test/feed.xml", source.FeedUrl);
    }
}
