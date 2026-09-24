namespace PersonalTechBrief.Application.Sources;

public sealed record CreateSourceCommand(string Name, string FeedUrl);

public sealed record UpdateSourceCommand(string Name, string FeedUrl, bool IsEnabled);
