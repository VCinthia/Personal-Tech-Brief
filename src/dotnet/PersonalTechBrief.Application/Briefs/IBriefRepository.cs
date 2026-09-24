using PersonalTechBrief.Domain.Briefs;

namespace PersonalTechBrief.Application.Briefs;

/// <summary>
/// Persistence boundary for briefs (FR-011/012/014, data model §11). Historical briefs are write-once;
/// implementations never rewrite a completed brief.
/// </summary>
public interface IBriefRepository
{
    /// <summary>The most recent completed brief's generation time, or null when none exists (§20 window start).</summary>
    Task<DateTime?> GetLatestCompletedBriefGeneratedAtUtcAsync(CancellationToken cancellationToken);

    /// <summary>Persists a new brief in <see cref="BriefStatus.Generating"/>.</summary>
    Task AddAsync(Brief brief, CancellationToken cancellationToken);

    /// <summary>Loads a tracked brief for completion, or null when it does not exist.</summary>
    Task<Brief?> GetForGenerationAsync(Guid briefId, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the eligible candidate updates for the window: score at or above <paramref name="threshold"/>,
    /// last observed at or after <paramref name="windowStartUtc"/>, active, and not already represented in a
    /// prior completed brief. Deterministic ordering is (re)applied by the selector.
    /// </summary>
    Task<IReadOnlyList<BriefCandidate>> LoadCandidatesAsync(
        DateTime windowStartUtc,
        double threshold,
        CancellationToken cancellationToken);

    /// <summary>Loads the persisted generation material (topic, supporting sources, matched interests) for a candidate.</summary>
    Task<BriefGenerationInput?> LoadGenerationInputAsync(Guid technologyUpdateId, CancellationToken cancellationToken);

    /// <summary>Persists pending changes (the completed brief and its immutable item snapshots).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Marks a still-generating brief as failed, discarding any staged item snapshots first so no
    /// partial brief is persisted. A no-op when the brief is missing or already left Generating.
    /// </summary>
    Task MarkFailedAsync(Guid briefId, CancellationToken cancellationToken);

    /// <summary>
    /// Fails every brief left in <see cref="BriefStatus.Generating"/> by a prior run (orphans, safe to
    /// abandon under the single-consumer model). Returns the number of briefs reconciled. Invoked once at
    /// startup before generation resumes so an orphan can never shadow the last completed brief.
    /// </summary>
    Task<int> FailOrphanedGeneratingBriefsAsync(DateTime createdBeforeUtc, CancellationToken cancellationToken);

    /// <summary>The latest <see cref="BriefStatus.Completed"/> brief, or null when none exists.</summary>
    Task<BriefReadModel?> GetCurrentAsync(CancellationToken cancellationToken);

    /// <summary>A specific brief by id, or null when it does not exist.</summary>
    Task<BriefReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>A page of brief history newest-first, paginating briefs (not the ingestion corpus).</summary>
    Task<BriefPage> ListAsync(string? cursor, int limit, CancellationToken cancellationToken);
}
