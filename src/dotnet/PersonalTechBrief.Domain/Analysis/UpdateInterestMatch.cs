namespace PersonalTechBrief.Domain.Analysis;

/// <summary>
/// A matched interest for a <see cref="TechnologyUpdate"/> (FR-009/010), retained as structured
/// rows so relevance is explainable without decoding opaque JSON. One row per
/// <c>(TechnologyUpdateId, InterestId)</c>; the match strength is the strongest observed across
/// the group's supporting items.
/// </summary>
public sealed class UpdateInterestMatch
{
    private UpdateInterestMatch()
    {
    }

    private UpdateInterestMatch(
        Guid technologyUpdateId,
        Guid interestId,
        double matchStrength,
        DateTime matchedAtUtc)
    {
        if (technologyUpdateId == Guid.Empty)
        {
            throw new ArgumentException("An interest match requires a technology update identifier.", nameof(technologyUpdateId));
        }

        if (interestId == Guid.Empty)
        {
            throw new ArgumentException("An interest match requires an interest identifier.", nameof(interestId));
        }

        TechnologyUpdateId = technologyUpdateId;
        InterestId = interestId;
        MatchStrength = ClampStrength(matchStrength);
        MatchedAtUtc = EnsureUtc(matchedAtUtc);
    }

    public Guid TechnologyUpdateId { get; private set; }

    public Guid InterestId { get; private set; }

    /// <summary>Clamped to [0, 1].</summary>
    public double MatchStrength { get; private set; }

    public DateTime MatchedAtUtc { get; private set; }

    public static UpdateInterestMatch Create(
        Guid technologyUpdateId,
        Guid interestId,
        double matchStrength,
        DateTime matchedAtUtc) =>
        new(technologyUpdateId, interestId, matchStrength, matchedAtUtc);

    /// <summary>
    /// Raises the recorded match strength when a later supporting item matches more strongly and
    /// records when that happened. Never lowers the strength, keeping merges monotonic.
    /// </summary>
    public void Reinforce(double matchStrength, DateTime matchedAtUtc)
    {
        var clamped = ClampStrength(matchStrength);
        if (clamped > MatchStrength)
        {
            MatchStrength = clamped;
        }

        MatchedAtUtc = EnsureUtc(matchedAtUtc);
    }

    private static double ClampStrength(double matchStrength)
    {
        if (double.IsNaN(matchStrength))
        {
            throw new ArgumentOutOfRangeException(nameof(matchStrength), "A match strength must be a number.");
        }

        return Math.Clamp(matchStrength, 0d, 1d);
    }

    private static DateTime EnsureUtc(DateTime matchedAtUtc)
    {
        if (matchedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("An interest match timestamp must be UTC.", nameof(matchedAtUtc));
        }

        return matchedAtUtc;
    }
}
