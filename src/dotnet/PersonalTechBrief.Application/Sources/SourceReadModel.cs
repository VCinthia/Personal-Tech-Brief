namespace PersonalTechBrief.Application.Sources;

public sealed record SourceReadModel(
    Guid Id,
    string Name,
    string FeedUrl,
    bool IsEnabled,
    DateTime? LastIngestionAtUtc,
    string? LastIngestionStatus);
