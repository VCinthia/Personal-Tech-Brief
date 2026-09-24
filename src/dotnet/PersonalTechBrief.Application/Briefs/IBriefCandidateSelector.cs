namespace PersonalTechBrief.Application.Briefs;

/// <summary>
/// Deterministic candidate selection (calibration §20), independent of the LLM and of infrastructure.
/// The same candidate set always yields the same selection.
/// </summary>
public interface IBriefCandidateSelector
{
    /// <summary>
    /// Selects the brief candidates from <paramref name="candidates"/>: keep those at or above
    /// <paramref name="threshold"/>, observed at or after the window start, and not already represented
    /// in a prior completed brief; rank by relevance score descending with a stable tie-break on the
    /// technology update id; take at most <paramref name="maxItems"/>. Unused slots are never padded and
    /// zero selections is a valid empty brief.
    /// </summary>
    BriefSelection Select(
        IReadOnlyList<BriefCandidate> candidates,
        DateTime windowStartUtc,
        double threshold,
        int maxItems);
}
