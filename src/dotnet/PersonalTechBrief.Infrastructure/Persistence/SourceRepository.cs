using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PersonalTechBrief.Application.Sources;
using PersonalTechBrief.Domain.Sources;

namespace PersonalTechBrief.Infrastructure.Persistence;

public sealed class SourceRepository(PersonalTechBriefDbContext dbContext) : ISourceRepository
{
    public async Task<IReadOnlyList<Source>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Sources
            .AsNoTracking()
            .OrderByDescending(source => source.IsEnabled)
            .ThenBy(source => source.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Source>> ListEnabledAsync(CancellationToken cancellationToken) =>
        await dbContext.Sources
            .AsNoTracking()
            .Where(source => source.IsEnabled)
            .OrderBy(source => source.Name)
            .ToListAsync(cancellationToken);

    public Task<Source?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Sources.SingleOrDefaultAsync(source => source.Id == id, cancellationToken);

    public Task<Source?> GetEnabledByNormalizedFeedUrlAsync(
        string normalizedFeedUrl,
        Guid? excludingId,
        CancellationToken cancellationToken) =>
        dbContext.Sources.SingleOrDefaultAsync(
            source =>
                source.IsEnabled &&
                source.NormalizedFeedUrl == normalizedFeedUrl &&
                (!excludingId.HasValue || source.Id != excludingId.Value),
            cancellationToken);

    public void Add(Source source) => dbContext.Sources.Add(source);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsEnabledSourceUrlConflict(exception))
        {
            throw new SourceConflictException("The enabled source feed URL is already in use.");
        }
    }

    private static bool IsEnabledSourceUrlConflict(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
