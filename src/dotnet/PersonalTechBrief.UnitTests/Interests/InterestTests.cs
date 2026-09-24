using PersonalTechBrief.Domain.Interests;

namespace PersonalTechBrief.UnitTests.Interests;

public class InterestTests
{
    private static readonly DateTime UtcNow = new(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_trims_the_display_name_and_normalizes_for_comparison()
    {
        var interest = Interest.Create("  .NET  ", InterestPriority.High, UtcNow);

        Assert.Equal(".NET", interest.Name);
        Assert.Equal(".NET", interest.NormalizedName);
        Assert.True(interest.IsActive);
        Assert.Equal(UtcNow, interest.CreatedAtUtc);
        Assert.Equal(UtcNow, interest.UpdatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_an_empty_name(string name)
    {
        Assert.Throws<ArgumentException>(() => Interest.Create(name, InterestPriority.Low, UtcNow));
    }

    [Fact]
    public void Create_rejects_a_name_longer_than_the_persistence_bound()
    {
        var name = new string('a', InterestName.MaxLength + 1);

        Assert.Throws<ArgumentException>(() => Interest.Create(name, InterestPriority.Low, UtcNow));
    }

    [Fact]
    public void Create_rejects_an_unsupported_priority()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Interest.Create("Platform engineering", (InterestPriority)99, UtcNow));
    }

    [Fact]
    public void Disable_preserves_preference_traceability_and_refreshes_the_timestamp()
    {
        var interest = Interest.Create("Cloud", InterestPriority.Medium, UtcNow);
        var disabledAt = UtcNow.AddMinutes(1);

        interest.Disable(disabledAt);

        Assert.False(interest.IsActive);
        Assert.Equal("Cloud", interest.Name);
        Assert.Equal("CLOUD", interest.NormalizedName);
        Assert.Equal(InterestPriority.Medium, interest.Priority);
        Assert.Equal(UtcNow, interest.CreatedAtUtc);
        Assert.Equal(disabledAt, interest.UpdatedAtUtc);
    }
}
