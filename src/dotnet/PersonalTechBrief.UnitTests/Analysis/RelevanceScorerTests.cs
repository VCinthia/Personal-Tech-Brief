using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Analysis;
using PersonalTechBrief.Domain.Interests;

namespace PersonalTechBrief.UnitTests.Analysis;

public sealed class RelevanceScorerTests
{
    private static readonly DateTime EvaluatedAtUtc = new(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(InterestPriority.High, 1.0, 60)]
    [InlineData(InterestPriority.High, 0.5, 30)]
    [InlineData(InterestPriority.Medium, 1.0, 40)]
    [InlineData(InterestPriority.Low, 1.0, 20)]
    public void Interest_base_uses_priority_weight_times_clamped_strength(
        InterestPriority priority, double strength, double expected)
    {
        var scorer = CreateScorer();

        var result = scorer.Score(Request(matches: [new InterestMatchSignal(priority, strength)]));

        Assert.Equal(expected, Component(result, RelevanceScorer.InterestComponentName).Points);
    }

    [Fact]
    public void Highest_priority_matched_interest_leads_even_when_a_lower_priority_matches_more_strongly()
    {
        var scorer = CreateScorer();

        // A weakly-matched High and a strongly-matched Medium: priority-first selects the High.
        var result = scorer.Score(Request(matches:
        [
            new InterestMatchSignal(InterestPriority.High, 0.1),
            new InterestMatchSignal(InterestPriority.Medium, 1.0),
        ]));

        // Base 60 * 0.1 (the High) + bonus 5 * 1.0 (the remaining Medium@1.0) = 11.
        Assert.Equal(11d, Component(result, RelevanceScorer.InterestComponentName).Points, 5);
    }

    [Fact]
    public void Ties_on_priority_break_by_the_stronger_match()
    {
        var scorer = CreateScorer();

        // Two High matches: the stronger strength sets the base; the other adds the bonus.
        var result = scorer.Score(Request(matches:
        [
            new InterestMatchSignal(InterestPriority.High, 0.4),
            new InterestMatchSignal(InterestPriority.High, 0.9),
        ]));

        // Base 60 * 0.9 (stronger High) + bonus 5 * 0.4 (the other High) = 56.
        Assert.Equal(56d, Component(result, RelevanceScorer.InterestComponentName).Points, 5);
    }

    [Fact]
    public void Additional_interest_bonus_is_capped_at_ten()
    {
        var scorer = CreateScorer();

        var result = scorer.Score(Request(matches:
        [
            new InterestMatchSignal(InterestPriority.High, 1.0),
            new InterestMatchSignal(InterestPriority.Medium, 1.0),
            new InterestMatchSignal(InterestPriority.Medium, 1.0),
            new InterestMatchSignal(InterestPriority.Low, 1.0),
        ]));

        // Base 60 + three additional (5*3 = 15) capped at 10 = 70.
        Assert.Equal(70d, Component(result, RelevanceScorer.InterestComponentName).Points);
    }

    [Theory]
    [InlineData(1.5, 60)]
    [InlineData(-0.5, 0)]
    public void Interest_strength_is_clamped_to_unit_interval(double strength, double expected)
    {
        var scorer = CreateScorer();

        var result = scorer.Score(Request(matches: [new InterestMatchSignal(InterestPriority.High, strength)]));

        Assert.Equal(expected, Component(result, RelevanceScorer.InterestComponentName).Points);
    }

    [Fact]
    public void No_matched_interests_yields_zero_interest_points()
    {
        var scorer = CreateScorer();

        var result = scorer.Score(Request(matches: []));

        Assert.Equal(0d, Component(result, RelevanceScorer.InterestComponentName).Points);
    }

    [Theory]
    [InlineData(0, 20)]      // now
    [InlineData(24, 20)]     // exactly 24h -> recent bucket
    [InlineData(25, 12)]     // just over 24h
    [InlineData(72, 12)]     // exactly 72h
    [InlineData(73, 5)]      // just over 72h
    [InlineData(168, 5)]     // exactly 7d
    [InlineData(169, 0)]     // older than 7d
    public void Recency_buckets_are_inclusive_of_their_upper_bound(int ageHours, double expected)
    {
        var scorer = CreateScorer();
        var newest = EvaluatedAtUtc.AddHours(-ageHours);

        var result = scorer.Score(Request(newestSupportingTimestampUtc: newest));

        Assert.Equal(expected, Component(result, RelevanceScorer.RecencyComponentName).Points);
    }

    [Fact]
    public void Recency_treats_a_future_timestamp_as_most_recent()
    {
        var scorer = CreateScorer();

        var result = scorer.Score(Request(newestSupportingTimestampUtc: EvaluatedAtUtc.AddHours(2)));

        Assert.Equal(20d, Component(result, RelevanceScorer.RecencyComponentName).Points);
    }

    [Theory]
    [InlineData(ImpactTier.High, 20)]
    [InlineData(ImpactTier.Medium, 10)]
    [InlineData(ImpactTier.Low, 0)]
    [InlineData(ImpactTier.None, 0)]
    public void Impact_maps_to_configured_points(ImpactTier impact, double expected)
    {
        var scorer = CreateScorer();

        var result = scorer.Score(Request(impact: impact));

        Assert.Equal(expected, Component(result, RelevanceScorer.ImpactComponentName).Points);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 4)]
    [InlineData(3, 7)]
    [InlineData(9, 7)]
    public void Source_support_buckets_on_distinct_host_count(int hosts, double expected)
    {
        var scorer = CreateScorer();

        var result = scorer.Score(Request(distinctSupportingHostCount: hosts));

        Assert.Equal(expected, Component(result, RelevanceScorer.SourceSupportComponentName).Points);
    }

    [Fact]
    public void Total_at_the_threshold_meets_selection()
    {
        var scorer = CreateScorer();

        // High@0.3 (18) + recent (20) + medium impact (10) + 3 hosts (7) = 55.
        var result = scorer.Score(Request(
            matches: [new InterestMatchSignal(InterestPriority.High, 0.3)],
            impact: ImpactTier.Medium,
            newestSupportingTimestampUtc: EvaluatedAtUtc,
            distinctSupportingHostCount: 3));

        Assert.Equal(55d, result.Total, 5);
        Assert.True(result.MeetsSelectionThreshold);
    }

    [Fact]
    public void Total_just_below_the_threshold_does_not_meet_selection()
    {
        var scorer = CreateScorer();

        // High@0.4 (24) + recent (20) + medium impact (10) + single host (0) = 54.
        var result = scorer.Score(Request(
            matches: [new InterestMatchSignal(InterestPriority.High, 0.4)],
            impact: ImpactTier.Medium,
            newestSupportingTimestampUtc: EvaluatedAtUtc,
            distinctSupportingHostCount: 1));

        Assert.Equal(54d, result.Total, 5);
        Assert.False(result.MeetsSelectionThreshold);
    }

    [Fact]
    public void Score_returns_all_four_named_components()
    {
        var scorer = CreateScorer();

        var result = scorer.Score(Request());

        Assert.Equal(4, result.Components.Count);
        Assert.Contains(result.Components, component => component.Name == RelevanceScorer.InterestComponentName);
        Assert.Contains(result.Components, component => component.Name == RelevanceScorer.RecencyComponentName);
        Assert.Contains(result.Components, component => component.Name == RelevanceScorer.ImpactComponentName);
        Assert.Contains(result.Components, component => component.Name == RelevanceScorer.SourceSupportComponentName);
        Assert.All(result.Components, component => Assert.False(string.IsNullOrWhiteSpace(component.Explanation)));
    }

    private static RelevanceScorer CreateScorer(RelevanceScoringOptions? options = null) =>
        new(Options.Create(options ?? new RelevanceScoringOptions()));

    private static RelevanceScoreRequest Request(
        IReadOnlyList<InterestMatchSignal>? matches = null,
        ImpactTier impact = ImpactTier.None,
        DateTime? newestSupportingTimestampUtc = null,
        int distinctSupportingHostCount = 1) =>
        new(
            matches ?? [],
            impact,
            newestSupportingTimestampUtc ?? EvaluatedAtUtc,
            EvaluatedAtUtc,
            distinctSupportingHostCount);

    private static RelevanceComponent Component(RelevanceScore score, string name) =>
        score.Components.Single(component => component.Name == name);
}
