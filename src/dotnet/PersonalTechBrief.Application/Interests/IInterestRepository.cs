using PersonalTechBrief.Domain.Interests;

namespace PersonalTechBrief.Application.Interests;

public interface IInterestRepository
{
    Task<IReadOnlyList<Interest>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Interest>> ListActiveAsync(CancellationToken cancellationToken);

    Task<Interest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Interest?> GetActiveByNormalizedNameAsync(
        string normalizedName,
        Guid? excludingId,
        CancellationToken cancellationToken);

    void Add(Interest interest);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
