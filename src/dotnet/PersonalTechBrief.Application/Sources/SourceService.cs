using PersonalTechBrief.Domain.Sources;

namespace PersonalTechBrief.Application.Sources;

public sealed class SourceService(
    ISourceRepository repository,
    IFeedValidator feedValidator,
    TimeProvider timeProvider) : ISourceService
{
    public async Task<IReadOnlyList<SourceReadModel>> ListAsync(CancellationToken cancellationToken)
    {
        var sources = await repository.ListAsync(cancellationToken);
        return sources.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<SourceReadModel>> ListEnabledAsync(CancellationToken cancellationToken)
    {
        var sources = await repository.ListEnabledAsync(cancellationToken);
        return sources.Select(Map).ToArray();
    }

    public async Task<SourceReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var source = await repository.GetByIdAsync(id, cancellationToken);
        return source is null ? null : Map(source);
    }

    public async Task<SourceReadModel> CreateAsync(CreateSourceCommand command, CancellationToken cancellationToken)
    {
        SourceName.Clean(command.Name);
        var feedUrl = SourceFeedUrl.Parse(command.FeedUrl);
        var normalizedFeedUrl = SourceFeedUrl.Normalize(command.FeedUrl);
        await EnsureNoEnabledDuplicateAsync(normalizedFeedUrl, excludingId: null, cancellationToken);
        await EnsureSupportedFeedAsync(feedUrl, cancellationToken);

        var source = Source.Create(command.Name, command.FeedUrl, UtcNow());
        repository.Add(source);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(source);
    }

    public async Task<SourceReadModel> UpdateAsync(
        Guid id,
        UpdateSourceCommand command,
        CancellationToken cancellationToken)
    {
        var source = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("The requested source does not exist.");

        SourceName.Clean(command.Name);
        var feedUrl = SourceFeedUrl.Parse(command.FeedUrl);
        var normalizedFeedUrl = SourceFeedUrl.Normalize(command.FeedUrl);
        if (command.IsEnabled)
        {
            await EnsureNoEnabledDuplicateAsync(normalizedFeedUrl, id, cancellationToken);
        }

        // A changed configured endpoint is always validated before being stored. Re-enabling the
        // same endpoint is also validated so a source cannot become active on stale validation.
        if (!string.Equals(source.NormalizedFeedUrl, normalizedFeedUrl, StringComparison.Ordinal) ||
            (command.IsEnabled && !source.IsEnabled))
        {
            await EnsureSupportedFeedAsync(feedUrl, cancellationToken);
        }

        source.Update(command.Name, command.FeedUrl, command.IsEnabled, UtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return Map(source);
    }

    public async Task<bool> EnableAsync(Guid id, CancellationToken cancellationToken)
    {
        var source = await repository.GetByIdAsync(id, cancellationToken);
        if (source is null)
        {
            return false;
        }

        await EnsureNoEnabledDuplicateAsync(source.NormalizedFeedUrl, id, cancellationToken);
        await EnsureSupportedFeedAsync(SourceFeedUrl.Parse(source.FeedUrl), cancellationToken);
        source.Enable(UtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DisableAsync(Guid id, CancellationToken cancellationToken)
    {
        var source = await repository.GetByIdAsync(id, cancellationToken);
        if (source is null)
        {
            return false;
        }

        source.Disable(UtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task EnsureNoEnabledDuplicateAsync(
        string normalizedFeedUrl,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var duplicate = await repository.GetEnabledByNormalizedFeedUrlAsync(
            normalizedFeedUrl,
            excludingId,
            cancellationToken);
        if (duplicate is not null)
        {
            throw new SourceConflictException(normalizedFeedUrl);
        }
    }

    private async Task EnsureSupportedFeedAsync(Uri feedUrl, CancellationToken cancellationToken)
    {
        if (!await feedValidator.IsSupportedAsync(feedUrl, cancellationToken))
        {
            throw new SourceFeedValidationException();
        }
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static SourceReadModel Map(Source source) =>
        new(
            source.Id,
            source.Name,
            source.FeedUrl,
            source.IsEnabled,
            source.LastIngestionAtUtc,
            source.LastIngestionStatus);
}
