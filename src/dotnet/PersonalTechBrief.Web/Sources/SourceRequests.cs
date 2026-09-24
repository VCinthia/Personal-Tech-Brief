namespace PersonalTechBrief.Web.Sources;

public sealed record CreateSourceRequest(string? Name, string? FeedUrl);

public sealed record UpdateSourceRequest(string? Name, string? FeedUrl, bool? IsEnabled);
