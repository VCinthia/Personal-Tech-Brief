namespace PersonalTechBrief.Domain.Interactions;

/// <summary>
/// Lightweight append-only telemetry recording that the user opened a supporting source of an update
/// (data model §11, FR-016). It is product-evaluation telemetry, not an engagement feed; the recording
/// path guards that the source item is a supporting source of the update before an event is created.
/// </summary>
public sealed class SourceOpenEvent
{
    private SourceOpenEvent()
    {
    }

    private SourceOpenEvent(Guid id, Guid technologyUpdateId, Guid sourceItemId, DateTime openedAtUtc)
    {
        if (technologyUpdateId == Guid.Empty)
        {
            throw new ArgumentException("A source-open event requires a technology update identifier.", nameof(technologyUpdateId));
        }

        if (sourceItemId == Guid.Empty)
        {
            throw new ArgumentException("A source-open event requires a source item identifier.", nameof(sourceItemId));
        }

        EnsureUtc(openedAtUtc, nameof(openedAtUtc));

        Id = id;
        TechnologyUpdateId = technologyUpdateId;
        SourceItemId = sourceItemId;
        OpenedAtUtc = openedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid TechnologyUpdateId { get; private set; }

    public Guid SourceItemId { get; private set; }

    public DateTime OpenedAtUtc { get; private set; }

    public static SourceOpenEvent Record(Guid technologyUpdateId, Guid sourceItemId, DateTime openedAtUtc) =>
        new(Guid.NewGuid(), technologyUpdateId, sourceItemId, openedAtUtc);

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Source-open event timestamps must be UTC.", parameterName);
        }
    }
}
