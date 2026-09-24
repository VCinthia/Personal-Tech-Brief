namespace PersonalTechBrief.Domain.Interactions;

/// <summary>
/// A single append-only relevance-feedback row for a <see cref="Analysis.TechnologyUpdate"/> (data model
/// §11, FR-013 subset). Rows are never rewritten; the "current" feedback for an update is the most recent
/// row, so a later row supersedes an earlier one for evaluation views (AC-012). The optional
/// <see cref="BriefItemId"/> attributes the feedback to the brief item the user acted on.
/// </summary>
public sealed class UserFeedback
{
    private UserFeedback()
    {
    }

    private UserFeedback(
        Guid id,
        Guid technologyUpdateId,
        Guid? briefItemId,
        FeedbackType feedbackType,
        DateTime createdAtUtc)
    {
        if (technologyUpdateId == Guid.Empty)
        {
            throw new ArgumentException("Feedback requires a technology update identifier.", nameof(technologyUpdateId));
        }

        if (briefItemId is { } value && value == Guid.Empty)
        {
            throw new ArgumentException("A brief item attribution, when present, must be a non-empty identifier.", nameof(briefItemId));
        }

        if (!Enum.IsDefined(feedbackType))
        {
            throw new ArgumentOutOfRangeException(nameof(feedbackType), "The feedback type is not a defined value.");
        }

        EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        Id = id;
        TechnologyUpdateId = technologyUpdateId;
        BriefItemId = briefItemId;
        FeedbackType = feedbackType;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid TechnologyUpdateId { get; private set; }

    /// <summary>The brief item the feedback was given on, when the action originated from a brief; null otherwise.</summary>
    public Guid? BriefItemId { get; private set; }

    public FeedbackType FeedbackType { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static UserFeedback Record(
        Guid technologyUpdateId,
        Guid? briefItemId,
        FeedbackType feedbackType,
        DateTime createdAtUtc) =>
        new(Guid.NewGuid(), technologyUpdateId, briefItemId, feedbackType, createdAtUtc);

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Feedback timestamps must be UTC.", parameterName);
        }
    }
}
