using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PersonalTechBrief.Application.Interests;
using PersonalTechBrief.Domain.Interests;

namespace PersonalTechBrief.Infrastructure.Persistence;

public sealed class InterestRepository(PersonalTechBriefDbContext dbContext) : IInterestRepository
{
    public async Task<IReadOnlyList<Interest>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Interests
            .AsNoTracking()
            .OrderByDescending(interest => interest.IsActive)
            .ThenBy(interest => interest.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Interest>> ListActiveAsync(CancellationToken cancellationToken) =>
        await dbContext.Interests
            .AsNoTracking()
            .Where(interest => interest.IsActive)
            .OrderBy(interest => interest.Name)
            .ToListAsync(cancellationToken);

    public Task<Interest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Interests.SingleOrDefaultAsync(interest => interest.Id == id, cancellationToken);

    public Task<Interest?> GetActiveByNormalizedNameAsync(
        string normalizedName,
        Guid? excludingId,
        CancellationToken cancellationToken) =>
        dbContext.Interests.SingleOrDefaultAsync(
            interest =>
                interest.IsActive &&
                interest.NormalizedName == normalizedName &&
                (!excludingId.HasValue || interest.Id != excludingId.Value),
            cancellationToken);

    public void Add(Interest interest) => dbContext.Interests.Add(interest);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsActiveInterestNameConflict(exception))
        {
            throw new InterestConflictException("The active interest name is already in use.");
        }
    }

    private static bool IsActiveInterestNameConflict(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
