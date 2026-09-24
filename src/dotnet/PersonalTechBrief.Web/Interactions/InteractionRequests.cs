namespace PersonalTechBrief.Web.Interactions;

/// <summary>
/// Request body for <c>PUT /api/v1/updates/{id}/feedback</c> (§12). <c>value</c> is <c>relevant</c> or
/// <c>notRelevant</c>; <c>briefItemId</c> optionally attributes the feedback to the brief item acted on.
/// </summary>
public sealed record FeedbackRequest(string? Value, Guid? BriefItemId);
