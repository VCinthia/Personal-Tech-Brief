using PersonalTechBrief.Domain.Sources;

namespace PersonalTechBrief.Application.Sources;

public interface ISourceRepository
{
    Task<IReadOnlyList<Source>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Loads the sources eligible for ingestion. Disabled sources remain persisted for traceability
    /// but are intentionally excluded from this query.
    /// </summary>
    Task<IReadOnlyList<Source>> ListEnabledAsync(CancellationToken cancellationToken);

    Task<Source?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Source?> GetEnabledByNormalizedFeedUrlAsync(
        string normalizedFeedUrl,
        Guid? excludingId,
        CancellationToken cancellationToken);

    void Add(Source source);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
