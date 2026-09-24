using PersonalTechBrief.Application.Messaging;
using PersonalTechBrief.Domain.Messaging;

namespace PersonalTechBrief.UnitTests.Messaging;

// Slice 8: the durable envelope/outbox carry a nullable source + ingestion run for source-less
// manual articles. A null is accepted; a present-but-empty identifier is still rejected.
public sealed class NullableSourceReferenceGuardTests
{
    private static readonly DateTime OccurredAtUtc = new(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Envelope_accepts_a_null_source_and_run_for_a_manual_item()
    {
        var envelope = SourceItemReadyEnvelope.Create(
            Guid.NewGuid(), Guid.NewGuid(), sourceId: null, ingestionRunId: null, Guid.NewGuid(), OccurredAtUtc, null);

        Assert.Null(envelope.SourceId);
        Assert.Null(envelope.IngestionRunId);
    }

    [Fact]
    public void Envelope_rejects_a_present_but_empty_source_or_run()
    {
        Assert.Throws<ArgumentException>(() => SourceItemReadyEnvelope.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), OccurredAtUtc, null));
        Assert.Throws<ArgumentException>(() => SourceItemReadyEnvelope.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), OccurredAtUtc, null));
    }

    [Fact]
    public void Outbox_accepts_a_null_source_and_run_for_a_manual_item()
    {
        var outbox = OutboxMessage.CreatePending(
            Guid.NewGuid(), Guid.NewGuid(), sourceId: null, ingestionRunId: null, Guid.NewGuid(),
            SourceItemReadyEnvelope.Destination, "{}", OccurredAtUtc);

        Assert.Null(outbox.SourceId);
        Assert.Null(outbox.IngestionRunId);
    }

    [Fact]
    public void Outbox_rejects_a_present_but_empty_source_or_run()
    {
        Assert.Throws<ArgumentException>(() => OutboxMessage.CreatePending(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid(),
            SourceItemReadyEnvelope.Destination, "{}", OccurredAtUtc));
        Assert.Throws<ArgumentException>(() => OutboxMessage.CreatePending(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid(),
            SourceItemReadyEnvelope.Destination, "{}", OccurredAtUtc));
    }
}
