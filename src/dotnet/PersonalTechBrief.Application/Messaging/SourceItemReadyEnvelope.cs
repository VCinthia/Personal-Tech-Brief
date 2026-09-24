using System.Text.Json;

namespace PersonalTechBrief.Application.Messaging;

/// <summary>
/// Version-one internal message body for the durable content-processing queue.
/// The outbox identifier is deliberately reused as the broker message identifier.
/// </summary>
public sealed record SourceItemReadyEnvelope(
    Guid MessageId,
    Guid SourceItemId,
    Guid? SourceId,
    Guid? IngestionRunId,
    Guid CorrelationId,
    DateTime OccurredAtUtc,
    string? TraceParent)
{
    public const string Destination = "content-processing";
    public const int TraceParentMaxLength = 512;

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    public static SourceItemReadyEnvelope Create(
        Guid messageId,
        Guid sourceItemId,
        Guid? sourceId,
        Guid? ingestionRunId,
        Guid correlationId,
        DateTime occurredAtUtc,
        string? traceParent)
    {
        if (messageId == Guid.Empty || sourceItemId == Guid.Empty || correlationId == Guid.Empty)
        {
            throw new ArgumentException("SourceItemReady requires non-empty identifiers.");
        }

        // A source-less manual article (FR-003) omits both source and ingestion run; a feed
        // item always supplies both. A present identifier must still be non-empty.
        if (sourceId == Guid.Empty || ingestionRunId == Guid.Empty)
        {
            throw new ArgumentException("SourceItemReady source and ingestion-run identifiers, when present, must be non-empty.");
        }

        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("SourceItemReady occurrence time must be UTC.", nameof(occurredAtUtc));
        }

        var cleanedTraceParent = string.IsNullOrWhiteSpace(traceParent)
            ? null
            : traceParent.Trim();
        if (cleanedTraceParent?.Length > TraceParentMaxLength)
        {
            throw new ArgumentException("Trace context must be bounded.", nameof(traceParent));
        }

        return new SourceItemReadyEnvelope(
            messageId,
            sourceItemId,
            sourceId,
            ingestionRunId,
            correlationId,
            occurredAtUtc,
            cleanedTraceParent);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
}
