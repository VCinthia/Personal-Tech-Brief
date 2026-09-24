namespace PersonalTechBrief.Domain.Interactions;

/// <summary>
/// The contracted subset of FR-013 relevance feedback a user may record on an update. Read/Pending and
/// Dismiss are explicitly out of scope for this slice, so only these two values exist.
/// </summary>
public enum FeedbackType
{
    Relevant,
    NotRelevant,
}
