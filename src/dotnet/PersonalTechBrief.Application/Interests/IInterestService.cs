namespace PersonalTechBrief.Application.Interests;

public interface IInterestService
{
    Task<IReadOnlyList<InterestReadModel>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<InterestReadModel>> ListActiveAsync(CancellationToken cancellationToken);

    Task<InterestReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<InterestReadModel> CreateAsync(CreateInterestCommand command, CancellationToken cancellationToken);

    Task<InterestReadModel> UpdateAsync(Guid id, UpdateInterestCommand command, CancellationToken cancellationToken);

    Task<bool> DisableAsync(Guid id, CancellationToken cancellationToken);
}
