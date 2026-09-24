using Microsoft.EntityFrameworkCore;
using PersonalTechBrief.Application.Interactions;
using PersonalTechBrief.Domain.Interactions;
using PersonalTechBrief.Infrastructure.Persistence;

namespace PersonalTechBrief.Infrastructure.Interactions;

/// <summary>
/// SQL-backed persistence for the FR-013 interaction subset (data model §11). Feedback and source-open
/// rows are appended; saves are keyed by update. None of these operations touch a Brief/BriefItem snapshot.
/// </summary>
public sealed class SqlInteractionRepository(PersonalTechBriefDbContext dbContext) : IInteractionRepository
{
    public Task<bool> UpdateExistsAsync(Guid technologyUpdateId, CancellationToken cancellationToken) =>
        dbContext.TechnologyUpdates.AsNoTracking().AnyAsync(update => update.Id == technologyUpdateId, cancellationToken);

    public Task<bool> BriefItemBelongsToUpdateAsync(
        Guid briefItemId,
        Guid technologyUpdateId,
        CancellationToken cancellationToken) =>
        dbContext.BriefItems.AsNoTracking().AnyAsync(
            item => item.Id == briefItemId && item.TechnologyUpdateId == technologyUpdateId,
            cancellationToken);

    public Task<bool> IsSupportingSourceAsync(
        Guid technologyUpdateId,
        Guid sourceItemId,
        CancellationToken cancellationToken) =>
        dbContext.TechnologyUpdateSources.AsNoTracking().AnyAsync(
            association => association.TechnologyUpdateId == technologyUpdateId
                && association.SourceItemId == sourceItemId,
            cancellationToken);

    public Task<bool> IsSavedAsync(Guid technologyUpdateId, CancellationToken cancellationToken) =>
        dbContext.SavedUpdates.AsNoTracking().AnyAsync(saved => saved.TechnologyUpdateId == technologyUpdateId, cancellationToken);

    public async Task AddFeedbackAsync(UserFeedback feedback, CancellationToken cancellationToken)
    {
        await dbContext.UserFeedback.AddAsync(feedback, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task ClearFeedbackAsync(Guid technologyUpdateId, CancellationToken cancellationToken) =>
        dbContext.UserFeedback
            .Where(feedback => feedback.TechnologyUpdateId == technologyUpdateId)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task AddSavedAsync(SavedUpdate saved, CancellationToken cancellationToken)
    {
        await dbContext.SavedUpdates.AddAsync(saved, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task RemoveSavedAsync(Guid technologyUpdateId, CancellationToken cancellationToken) =>
        dbContext.SavedUpdates
            .Where(saved => saved.TechnologyUpdateId == technologyUpdateId)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task AddSourceOpenAsync(SourceOpenEvent sourceOpen, CancellationToken cancellationToken)
    {
        await dbContext.SourceOpenEvents.AddAsync(sourceOpen, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
