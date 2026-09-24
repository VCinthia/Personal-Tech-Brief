using PersonalTechBrief.Domain.Interests;

namespace PersonalTechBrief.Application.Interests;

public sealed class InterestService(IInterestRepository repository, TimeProvider timeProvider) : IInterestService
{
    public async Task<IReadOnlyList<InterestReadModel>> ListAsync(CancellationToken cancellationToken)
    {
        var interests = await repository.ListAsync(cancellationToken);
        return interests.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<InterestReadModel>> ListActiveAsync(CancellationToken cancellationToken)
    {
        var interests = await repository.ListActiveAsync(cancellationToken);
        return interests.Select(Map).ToArray();
    }

    public async Task<InterestReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var interest = await repository.GetByIdAsync(id, cancellationToken);
        return interest is null ? null : Map(interest);
    }

    public async Task<InterestReadModel> CreateAsync(CreateInterestCommand command, CancellationToken cancellationToken)
    {
        var normalizedName = InterestName.Normalize(command.Name);
        await EnsureNoActiveDuplicateAsync(normalizedName, excludingId: null, cancellationToken);

        var interest = Interest.Create(command.Name, command.Priority, UtcNow());
        repository.Add(interest);
        await repository.SaveChangesAsync(cancellationToken);

        return Map(interest);
    }

    public async Task<InterestReadModel> UpdateAsync(
        Guid id,
        UpdateInterestCommand command,
        CancellationToken cancellationToken)
    {
        var interest = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("The requested interest does not exist.");

        var normalizedName = InterestName.Normalize(command.Name);
        if (command.IsActive)
        {
            await EnsureNoActiveDuplicateAsync(normalizedName, id, cancellationToken);
        }

        interest.Update(command.Name, command.Priority, command.IsActive, UtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        return Map(interest);
    }

    public async Task<bool> DisableAsync(Guid id, CancellationToken cancellationToken)
    {
        var interest = await repository.GetByIdAsync(id, cancellationToken);
        if (interest is null)
        {
            return false;
        }

        interest.Disable(UtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task EnsureNoActiveDuplicateAsync(
        string normalizedName,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var duplicate = await repository.GetActiveByNormalizedNameAsync(normalizedName, excludingId, cancellationToken);
        if (duplicate is not null)
        {
            throw new InterestConflictException(normalizedName);
        }
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static InterestReadModel Map(Interest interest) =>
        new(
            interest.Id,
            interest.Name,
            interest.Priority,
            interest.IsActive,
            interest.CreatedAtUtc,
            interest.UpdatedAtUtc);
}
