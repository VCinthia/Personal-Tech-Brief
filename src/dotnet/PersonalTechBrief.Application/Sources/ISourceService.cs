namespace PersonalTechBrief.Application.Sources;

public interface ISourceService
{
    Task<IReadOnlyList<SourceReadModel>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SourceReadModel>> ListEnabledAsync(CancellationToken cancellationToken);

    Task<SourceReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<SourceReadModel> CreateAsync(CreateSourceCommand command, CancellationToken cancellationToken);

    Task<SourceReadModel> UpdateAsync(Guid id, UpdateSourceCommand command, CancellationToken cancellationToken);

    Task<bool> EnableAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> DisableAsync(Guid id, CancellationToken cancellationToken);
}
