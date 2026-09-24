namespace PersonalTechBrief.Application.Ingestion;

public sealed class IngestionDeduplicationOptions
{
    public const string SectionName = "Ingestion:Deduplication";

    /// <summary>
    /// Fallback duplicate horizon used only when external identifier, URL, and content hash are absent.
    /// </summary>
    public int TitleWindowDays { get; init; } = 7;
}
