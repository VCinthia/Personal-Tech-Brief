using PersonalTechBrief.Domain.Interactions;

namespace PersonalTechBrief.Application.Interactions;

/// <summary>
/// Parses the §12 feedback wire value (<c>relevant</c> / <c>notRelevant</c>) to and from
/// <see cref="FeedbackType"/>. Kept out of the endpoint so the boundary validation is unit-testable.
/// </summary>
public static class FeedbackValue
{
    public const string Relevant = "relevant";
    public const string NotRelevant = "notRelevant";

    public static bool TryParse(string? value, out FeedbackType feedbackType)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "relevant":
                feedbackType = FeedbackType.Relevant;
                return true;
            case "notrelevant":
                feedbackType = FeedbackType.NotRelevant;
                return true;
            default:
                feedbackType = default;
                return false;
        }
    }

    public static string ToWireValue(FeedbackType feedbackType) =>
        feedbackType == FeedbackType.Relevant ? Relevant : NotRelevant;
}
