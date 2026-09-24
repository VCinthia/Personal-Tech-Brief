namespace PersonalTechBrief.Application.Briefs;

/// <summary>
/// Orchestrates asynchronous generation of a brief already created in <c>Generating</c> (FR-011/018,
/// UC-007/008, pipeline §13 stages 9-11). It selects candidates deterministically, generates each in
/// rank order via the Intelligence API with bounded transient retry, persists an immutable snapshot on
/// success, excludes a candidate on persistent failure (never invents or pads), and completes the
/// brief with the generated items (possibly fewer, possibly zero). Hosting-independent and testable.
/// </summary>
public interface IBriefGenerationService
{
    /// <summary>Generates the brief with the given id and transitions it to Completed or Failed.</summary>
    Task GenerateAsync(Guid briefId, CancellationToken cancellationToken);
}
