using PersonalTechBrief.Domain.Interactions;

namespace PersonalTechBrief.UnitTests.Interactions;

// Domain invariants for the interaction entities: UTC-only timestamps and non-empty identifiers.
public class InteractionEntitiesTests
{
    private static readonly DateTime Utc = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Local = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Local);
    private static readonly Guid UpdateId = Guid.NewGuid();
    private static readonly Guid SourceItemId = Guid.NewGuid();

    [Fact]
    public void UserFeedback_records_a_valid_row()
    {
        var feedback = UserFeedback.Record(UpdateId, briefItemId: null, FeedbackType.NotRelevant, Utc);

        Assert.NotEqual(Guid.Empty, feedback.Id);
        Assert.Equal(UpdateId, feedback.TechnologyUpdateId);
        Assert.Null(feedback.BriefItemId);
        Assert.Equal(FeedbackType.NotRelevant, feedback.FeedbackType);
    }

    [Fact]
    public void UserFeedback_rejects_a_non_utc_timestamp() =>
        Assert.Throws<ArgumentException>(() => UserFeedback.Record(UpdateId, null, FeedbackType.Relevant, Local));

    [Fact]
    public void UserFeedback_rejects_an_empty_update_id() =>
        Assert.Throws<ArgumentException>(() => UserFeedback.Record(Guid.Empty, null, FeedbackType.Relevant, Utc));

    [Fact]
    public void SavedUpdate_rejects_a_non_utc_timestamp() =>
        Assert.Throws<ArgumentException>(() => SavedUpdate.Create(UpdateId, Local));

    [Fact]
    public void SavedUpdate_rejects_an_empty_update_id() =>
        Assert.Throws<ArgumentException>(() => SavedUpdate.Create(Guid.Empty, Utc));

    [Fact]
    public void SourceOpenEvent_rejects_a_non_utc_timestamp() =>
        Assert.Throws<ArgumentException>(() => SourceOpenEvent.Record(UpdateId, SourceItemId, Local));

    [Fact]
    public void SourceOpenEvent_rejects_empty_identifiers()
    {
        Assert.Throws<ArgumentException>(() => SourceOpenEvent.Record(Guid.Empty, SourceItemId, Utc));
        Assert.Throws<ArgumentException>(() => SourceOpenEvent.Record(UpdateId, Guid.Empty, Utc));
    }
}
