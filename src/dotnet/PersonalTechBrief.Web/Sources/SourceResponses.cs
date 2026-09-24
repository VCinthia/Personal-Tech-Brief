using PersonalTechBrief.Application.Sources;

namespace PersonalTechBrief.Web.Sources;

public sealed record SourceResponse(
    Guid Id,
    string Name,
    string FeedUrl,
    bool IsEnabled,
    DateTime? LastIngestionAtUtc,
    string? LastIngestionStatus)
{
    public static SourceResponse From(SourceReadModel source) =>
        new(
            source.Id,
            source.Name,
            source.FeedUrl,
            source.IsEnabled,
            source.LastIngestionAtUtc,
            source.LastIngestionStatus);
}
