using PersonalTechBrief.Application.Briefs;

namespace PersonalTechBrief.UnitTests.Briefs;

public class BriefCandidateSelectorTests
{
    private static readonly DateTime WindowStartUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const double Threshold = 55d;
    private const int MaxItems = 5;

    private readonly BriefCandidateSelector _selector = new();

    private static BriefCandidate Candidate(
        string id,
        double score,
        DateTime? lastObserved = null,
        bool alreadyBriefed = false) =>
        new(Guid.Parse(id), score, lastObserved ?? WindowStartUtc.AddHours(1), alreadyBriefed);

    [Fact]
    public void Excludes_candidates_below_the_threshold()
    {
        var candidates = new[]
        {
            Candidate("11111111-1111-1111-1111-111111111111", 54.9d),
            Candidate("22222222-2222-2222-2222-222222222222", 55d),
        };

        var selection = _selector.Select(candidates, WindowStartUtc, Threshold, MaxItems);

        Assert.Equal(1, selection.CandidateCount);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), Assert.Single(selection.Selected).TechnologyUpdateId);
    }

    [Fact]
    public void Ranks_by_score_descending_then_by_id_ascending()
    {
        var candidates = new[]
        {
            Candidate("33333333-3333-3333-3333-333333333333", 70d),
            Candidate("11111111-1111-1111-1111-111111111111", 90d),
            // Tie at 70: the lower id ranks first.
            Candidate("22222222-2222-2222-2222-222222222222", 70d),
        };

        var selection = _selector.Select(candidates, WindowStartUtc, Threshold, MaxItems);

        Assert.Equal(
            [
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ],
            selection.Selected.Select(candidate => candidate.TechnologyUpdateId));
    }

    [Fact]
    public void Takes_at_most_the_maximum_and_never_pads()
    {
        var candidates = Enumerable.Range(1, 8)
            .Select(index => Candidate($"{index:D8}-0000-0000-0000-000000000000", 60d + index))
            .ToList();

        var selection = _selector.Select(candidates, WindowStartUtc, Threshold, MaxItems);

        Assert.Equal(8, selection.CandidateCount);
        Assert.Equal(MaxItems, selection.Selected.Count);
    }

    [Fact]
    public void Returns_all_eligible_when_fewer_than_the_maximum()
    {
        var candidates = new[]
        {
            Candidate("11111111-1111-1111-1111-111111111111", 60d),
            Candidate("22222222-2222-2222-2222-222222222222", 80d),
        };

        var selection = _selector.Select(candidates, WindowStartUtc, Threshold, MaxItems);

        Assert.Equal(2, selection.Selected.Count);
    }

    [Fact]
    public void Empty_candidate_set_is_a_valid_empty_selection()
    {
        var selection = _selector.Select([], WindowStartUtc, Threshold, MaxItems);

        Assert.Equal(0, selection.CandidateCount);
        Assert.Empty(selection.Selected);
    }

    [Fact]
    public void Excludes_candidates_already_represented_in_a_prior_completed_brief()
    {
        var candidates = new[]
        {
            Candidate("11111111-1111-1111-1111-111111111111", 90d, alreadyBriefed: true),
            Candidate("22222222-2222-2222-2222-222222222222", 60d),
        };

        var selection = _selector.Select(candidates, WindowStartUtc, Threshold, MaxItems);

        Assert.Equal(1, selection.CandidateCount);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), Assert.Single(selection.Selected).TechnologyUpdateId);
    }

    [Fact]
    public void Excludes_candidates_observed_before_the_window_start()
    {
        var candidates = new[]
        {
            Candidate("11111111-1111-1111-1111-111111111111", 90d, lastObserved: WindowStartUtc.AddSeconds(-1)),
            Candidate("22222222-2222-2222-2222-222222222222", 60d, lastObserved: WindowStartUtc),
        };

        var selection = _selector.Select(candidates, WindowStartUtc, Threshold, MaxItems);

        Assert.Equal(1, selection.CandidateCount);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), Assert.Single(selection.Selected).TechnologyUpdateId);
    }
}
