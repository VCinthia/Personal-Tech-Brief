namespace PersonalTechBrief.Domain.Interactions;

/// <summary>
/// A saved-for-later marker on a <see cref="Analysis.TechnologyUpdate"/> (data model §11, FR-013 subset,
/// AC-013). The update identifier is the primary key, so saving is idempotent; unsaving deletes the row.
/// Single-user MVP, so no user identifier is stored.
/// </summary>
public sealed class SavedUpdate
{
    private SavedUpdate()
    {
    }

    private SavedUpdate(Guid technologyUpdateId, DateTime savedAtUtc)
    {
        if (technologyUpdateId == Guid.Empty)
        {
            throw new ArgumentException("A saved update requires a technology update identifier.", nameof(technologyUpdateId));
        }

        EnsureUtc(savedAtUtc, nameof(savedAtUtc));

        TechnologyUpdateId = technologyUpdateId;
        SavedAtUtc = savedAtUtc;
    }

    public Guid TechnologyUpdateId { get; private set; }

    public DateTime SavedAtUtc { get; private set; }

    public static SavedUpdate Create(Guid technologyUpdateId, DateTime savedAtUtc) =>
        new(technologyUpdateId, savedAtUtc);

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Saved update timestamps must be UTC.", parameterName);
        }
    }
}
