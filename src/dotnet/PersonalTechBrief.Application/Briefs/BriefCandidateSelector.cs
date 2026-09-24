namespace PersonalTechBrief.Application.Briefs;

/// <inheritdoc />
public sealed class BriefCandidateSelector : IBriefCandidateSelector
{
    public BriefSelection Select(
        IReadOnlyList<BriefCandidate> candidates,
        DateTime windowStartUtc,
        double threshold,
        int maxItems)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (maxItems < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxItems), "The maximum item count must not be negative.");
        }

        var eligible = candidates
            .Where(candidate => !candidate.AlreadyBriefed)
            .Where(candidate => candidate.LastObservedAtUtc >= windowStartUtc)
            .Where(candidate => candidate.RelevanceScore >= threshold)
            .OrderByDescending(candidate => candidate.RelevanceScore)
            .ThenBy(candidate => candidate.TechnologyUpdateId)
            .ToList();

        var selected = eligible.Take(maxItems).ToList();
        return new BriefSelection(eligible.Count, selected);
    }
}
