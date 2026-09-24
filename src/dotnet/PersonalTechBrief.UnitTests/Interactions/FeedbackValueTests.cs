using PersonalTechBrief.Application.Interactions;
using PersonalTechBrief.Domain.Interactions;

namespace PersonalTechBrief.UnitTests.Interactions;

// Boundary value validation for the §12 feedback wire value (AC-012).
public class FeedbackValueTests
{
    [Theory]
    [InlineData("relevant", FeedbackType.Relevant)]
    [InlineData("Relevant", FeedbackType.Relevant)]
    [InlineData("  relevant  ", FeedbackType.Relevant)]
    [InlineData("notRelevant", FeedbackType.NotRelevant)]
    [InlineData("NOTRELEVANT", FeedbackType.NotRelevant)]
    public void TryParse_accepts_the_contracted_values(string value, FeedbackType expected)
    {
        Assert.True(FeedbackValue.TryParse(value, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("maybe")]
    [InlineData("read")]
    [InlineData("dismiss")]
    public void TryParse_rejects_unknown_values(string? value)
    {
        Assert.False(FeedbackValue.TryParse(value, out _));
    }

    [Theory]
    [InlineData(FeedbackType.Relevant, "relevant")]
    [InlineData(FeedbackType.NotRelevant, "notRelevant")]
    public void ToWireValue_round_trips_the_contracted_values(FeedbackType feedbackType, string expected)
    {
        Assert.Equal(expected, FeedbackValue.ToWireValue(feedbackType));
    }
}
