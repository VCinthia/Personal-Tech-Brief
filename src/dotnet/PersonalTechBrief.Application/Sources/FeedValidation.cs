namespace PersonalTechBrief.Application.Sources;

/// <summary>
/// Validates whether a syntactically safe source endpoint exposes a supported RSS or Atom feed.
/// It is a source-management boundary, not the periodic ingestion/retrieval pipeline.
/// </summary>
public interface IFeedValidator
{
    Task<bool> IsSupportedAsync(Uri feedUrl, CancellationToken cancellationToken);
}
