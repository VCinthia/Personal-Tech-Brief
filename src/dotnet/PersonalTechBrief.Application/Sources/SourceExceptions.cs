namespace PersonalTechBrief.Application.Sources;

public sealed class SourceConflictException(string normalizedFeedUrl) : Exception("An enabled source with this feed URL already exists.")
{
    public string NormalizedFeedUrl { get; } = normalizedFeedUrl;
}

public sealed class SourceFeedValidationException : Exception
{
    public SourceFeedValidationException()
        : base("The feed URL could not be validated as a supported RSS or Atom feed.")
    {
    }
}
