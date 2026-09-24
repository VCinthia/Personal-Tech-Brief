namespace PersonalTechBrief.Application.Analysis;

/// <summary>
/// Deterministic relevance scorer (calibration §20). Pure and side-effect free: the same request
/// always yields the same score and breakdown. No I/O and no live model call (architecture invariant:
/// deterministic rules must not require a live LLM).
/// </summary>
public interface IRelevanceScorer
{
    RelevanceScore Score(RelevanceScoreRequest request);
}
