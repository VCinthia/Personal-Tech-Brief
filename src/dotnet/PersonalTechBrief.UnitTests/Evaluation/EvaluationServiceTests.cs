using PersonalTechBrief.Application.Evaluation;
using PersonalTechBrief.Domain.Interactions;

namespace PersonalTechBrief.UnitTests.Evaluation;

// Unit coverage for the FR-016 evaluation aggregation: each derived signal computed from raw signals via
// a fake repository (no database). Latest feedback supersedes for the relevant/not-relevant tally.
public class EvaluationServiceTests
{
    private static readonly Guid UpdateA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UpdateB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SourceX = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTime BaseUtc = new(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Passes_through_raw_counts_and_distribution()
    {
        var distribution = new List<SourceDistributionItem>
        {
            new(SourceX, "Feed X", IngestedItems: 10, SelectedItems: 3),
        };
        var signals = new EvaluationSignals(
            ItemsIngested: 42,
            ItemsSelected: 8,
            TotalSupportingSourceLinks: 0,
            UpdatesWithSupportingSources: 0,
            SourceOpens: 5,
            Saves: 2,
            CompletedBriefCount: 4,
            Feedback: [],
            SourceDistribution: distribution);

        var summary = await Service(signals).GetSummaryAsync(CancellationToken.None);

        Assert.Equal(42, summary.ItemsIngested);
        Assert.Equal(8, summary.ItemsSelected);
        Assert.Equal(5, summary.SourceOpens);
        Assert.Equal(2, summary.Saves);
        Assert.Equal(4, summary.BriefCount);
        Assert.Same(distribution, summary.SourceDistribution);
    }

    [Fact]
    public async Task Grouped_duplicates_counts_supporting_links_beyond_the_first_per_update()
    {
        // 5 links spread over 2 updates => 3 duplicates folded in.
        var signals = EmptySignals() with { TotalSupportingSourceLinks = 5, UpdatesWithSupportingSources = 2 };

        var summary = await Service(signals).GetSummaryAsync(CancellationToken.None);

        Assert.Equal(3, summary.GroupedDuplicates);
    }

    [Fact]
    public async Task Grouped_duplicates_never_goes_negative()
    {
        var signals = EmptySignals() with { TotalSupportingSourceLinks = 0, UpdatesWithSupportingSources = 0 };

        var summary = await Service(signals).GetSummaryAsync(CancellationToken.None);

        Assert.Equal(0, summary.GroupedDuplicates);
    }

    [Fact]
    public async Task Feedback_tally_uses_the_latest_row_per_update()
    {
        // Update A: relevant then not-relevant (latest wins). Update B: relevant.
        var feedback = new List<FeedbackRecord>
        {
            new(UpdateA, Guid.NewGuid(), FeedbackType.Relevant, BaseUtc),
            new(UpdateA, Guid.NewGuid(), FeedbackType.NotRelevant, BaseUtc.AddMinutes(5)),
            new(UpdateB, Guid.NewGuid(), FeedbackType.Relevant, BaseUtc.AddMinutes(1)),
        };
        var signals = EmptySignals() with { Feedback = feedback };

        var summary = await Service(signals).GetSummaryAsync(CancellationToken.None);

        Assert.Equal(1, summary.RelevantFeedback);
        Assert.Equal(1, summary.NotRelevantFeedback);
    }

    [Fact]
    public async Task Items_per_brief_is_the_average_and_zero_when_no_completed_briefs()
    {
        var withBriefs = EmptySignals() with { ItemsSelected = 9, CompletedBriefCount = 4 };
        Assert.Equal(2.25d, (await Service(withBriefs).GetSummaryAsync(CancellationToken.None)).ItemsPerBrief);

        var noBriefs = EmptySignals() with { ItemsSelected = 0, CompletedBriefCount = 0 };
        Assert.Equal(0d, (await Service(noBriefs).GetSummaryAsync(CancellationToken.None)).ItemsPerBrief);
    }

    private static EvaluationSignals EmptySignals() => new(
        ItemsIngested: 0,
        ItemsSelected: 0,
        TotalSupportingSourceLinks: 0,
        UpdatesWithSupportingSources: 0,
        SourceOpens: 0,
        Saves: 0,
        CompletedBriefCount: 0,
        Feedback: [],
        SourceDistribution: []);

    private static EvaluationService Service(EvaluationSignals signals) =>
        new(new FakeEvaluationRepository(signals));

    private sealed class FakeEvaluationRepository(EvaluationSignals signals) : IEvaluationRepository
    {
        public Task<EvaluationSignals> GetSignalsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(signals);
    }
}
