using System.Globalization;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Domain.Interests;

namespace PersonalTechBrief.Application.Analysis;

/// <summary>
/// Deterministic implementation of the calibration §20 relevance model. Weights and thresholds are
/// configuration (<see cref="RelevanceScoringOptions"/>); nothing here is hardcoded product policy.
/// </summary>
public sealed class RelevanceScorer(IOptions<RelevanceScoringOptions> options) : IRelevanceScorer
{
    public const string InterestComponentName = "Interest";
    public const string RecencyComponentName = "Recency";
    public const string ImpactComponentName = "Impact";
    public const string SourceSupportComponentName = "SourceSupport";

    private readonly RelevanceScoringOptions options = options.Value;

    public RelevanceScore Score(RelevanceScoreRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var interest = ScoreInterest(request.InterestMatches);
        var recency = ScoreRecency(request.NewestSupportingTimestampUtc, request.EvaluatedAtUtc);
        var impact = ScoreImpact(request.Impact);
        var sourceSupport = ScoreSourceSupport(request.DistinctSupportingHostCount);

        var total = interest.Points + recency.Points + impact.Points + sourceSupport.Points;
        return new RelevanceScore(
            total,
            total >= options.SelectionThreshold,
            [interest, recency, impact, sourceSupport]);
    }

    private RelevanceComponent ScoreInterest(IReadOnlyList<InterestMatchSignal> matches)
    {
        if (matches is null || matches.Count == 0)
        {
            return new RelevanceComponent(InterestComponentName, 0d, "No matched active interests.");
        }

        // Priority-first (§20 "the strongest matched active interest"): the highest-priority matched
        // interest sets the base component, with ties on priority broken by the stronger match, so a
        // high-priority interest the user configured leads even when a lower-priority interest matches
        // more strongly. Every other matched interest adds a small capped bonus.
        var ranked = matches
            .Select(match => new
            {
                match.Priority,
                Strength = Math.Clamp(match.MatchStrength, 0d, 1d),
            })
            .OrderByDescending(match => PriorityRank(match.Priority))
            .ThenByDescending(match => match.Strength)
            .ToList();

        var strongest = ranked[0];
        var basePoints = PriorityWeight(strongest.Priority) * strongest.Strength;

        var bonus = Math.Min(
            options.AdditionalInterestBonusCap,
            ranked.Skip(1).Sum(match => options.AdditionalInterestPointsPerMatch * match.Strength));

        var points = basePoints + bonus;
        var explanation = string.Create(
            CultureInfo.InvariantCulture,
            $"Highest-priority matched interest {strongest.Priority} at strength {strongest.Strength:0.###} = {basePoints:0.###}; {ranked.Count - 1} additional matched interest(s) add a capped bonus of {bonus:0.###}.");
        return new RelevanceComponent(InterestComponentName, points, explanation);
    }

    private RelevanceComponent ScoreRecency(DateTime newestSupportingTimestampUtc, DateTime evaluatedAtUtc)
    {
        var age = evaluatedAtUtc - newestSupportingTimestampUtc;

        if (age <= TimeSpan.FromHours(options.RecencyRecentHours))
        {
            return new RelevanceComponent(
                RecencyComponentName,
                options.RecencyRecentPoints,
                $"Newest supporting source within {options.RecencyRecentHours}h.");
        }

        if (age <= TimeSpan.FromHours(options.RecencyRecentDaysHours))
        {
            return new RelevanceComponent(
                RecencyComponentName,
                options.RecencyRecentDaysPoints,
                $"Newest supporting source within {options.RecencyRecentDaysHours}h.");
        }

        if (age <= TimeSpan.FromHours(options.RecencyWeekHours))
        {
            return new RelevanceComponent(
                RecencyComponentName,
                options.RecencyWeekPoints,
                $"Newest supporting source within {options.RecencyWeekHours}h.");
        }

        return new RelevanceComponent(
            RecencyComponentName,
            0d,
            $"Newest supporting source older than {options.RecencyWeekHours}h.");
    }

    private RelevanceComponent ScoreImpact(ImpactTier impact) => impact switch
    {
        ImpactTier.High => new RelevanceComponent(ImpactComponentName, options.ImpactHighPoints, "High analyzed impact."),
        ImpactTier.Medium => new RelevanceComponent(ImpactComponentName, options.ImpactMediumPoints, "Medium analyzed impact."),
        _ => new RelevanceComponent(ImpactComponentName, 0d, "Low or unknown analyzed impact."),
    };

    private RelevanceComponent ScoreSourceSupport(int distinctSupportingHostCount)
    {
        if (distinctSupportingHostCount >= 3)
        {
            return new RelevanceComponent(
                SourceSupportComponentName,
                options.SourceSupportThreeOrMoreHostsPoints,
                $"{distinctSupportingHostCount} distinct supporting hosts.");
        }

        if (distinctSupportingHostCount == 2)
        {
            return new RelevanceComponent(
                SourceSupportComponentName,
                options.SourceSupportTwoHostsPoints,
                "2 distinct supporting hosts.");
        }

        return new RelevanceComponent(
            SourceSupportComponentName,
            0d,
            "Single supporting host.");
    }

    private double PriorityWeight(InterestPriority priority) => priority switch
    {
        InterestPriority.High => options.InterestHighWeight,
        InterestPriority.Medium => options.InterestMediumWeight,
        InterestPriority.Low => options.InterestLowWeight,
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unsupported interest priority."),
    };

    // Priority ordering is independent of the (configurable, possibly-equal) weights so selection
    // stays strictly High > Medium > Low even if two priority weights are configured equal.
    private static int PriorityRank(InterestPriority priority) => priority switch
    {
        InterestPriority.High => 3,
        InterestPriority.Medium => 2,
        InterestPriority.Low => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unsupported interest priority."),
    };
}
