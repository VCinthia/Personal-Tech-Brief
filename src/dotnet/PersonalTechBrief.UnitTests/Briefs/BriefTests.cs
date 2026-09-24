using PersonalTechBrief.Domain.Briefs;

namespace PersonalTechBrief.UnitTests.Briefs;

public class BriefTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    private static Brief NewGeneratingBrief() =>
        Brief.Create(NowUtc, NowUtc.AddDays(-7), NowUtc, "brief-1", Guid.NewGuid());

    private static BriefItem NewItem(Brief brief, int rank, Guid? updateId = null) =>
        BriefItem.Create(
            brief.Id,
            updateId ?? Guid.NewGuid(),
            rank,
            "Title",
            "Topic",
            "Summary",
            "Why relevant",
            72d,
            NowUtc,
            "generate-1",
            "generate-1",
            [new BriefItemSourceSnapshot(Guid.NewGuid(), "Source", "https://example.test/a", NowUtc)]);

    [Fact]
    public void Complete_sets_completed_status_and_counts()
    {
        var brief = NewGeneratingBrief();
        var items = new[] { NewItem(brief, 1), NewItem(brief, 2) };

        brief.Complete(candidateCount: 4, items);

        Assert.Equal(BriefStatus.Completed, brief.Status);
        Assert.Equal(4, brief.CandidateCount);
        Assert.Equal(2, brief.SelectedCount);
        Assert.Equal(2, brief.Items.Count);
    }

    [Fact]
    public void Complete_with_zero_items_is_valid()
    {
        var brief = NewGeneratingBrief();

        brief.Complete(candidateCount: 0, []);

        Assert.Equal(BriefStatus.Completed, brief.Status);
        Assert.Equal(0, brief.SelectedCount);
        Assert.Empty(brief.Items);
    }

    [Fact]
    public void A_completed_brief_is_immutable_and_cannot_be_completed_again()
    {
        var brief = NewGeneratingBrief();
        brief.Complete(candidateCount: 1, [NewItem(brief, 1)]);

        Assert.Throws<InvalidOperationException>(() => brief.Complete(2, [NewItem(brief, 1)]));
        Assert.Throws<InvalidOperationException>(brief.Fail);
    }

    [Fact]
    public void A_failed_brief_is_immutable_and_cannot_be_completed()
    {
        var brief = NewGeneratingBrief();
        brief.Fail();

        Assert.Equal(BriefStatus.Failed, brief.Status);
        Assert.Throws<InvalidOperationException>(() => brief.Complete(0, []));
    }

    [Fact]
    public void Complete_rejects_duplicate_ranks()
    {
        var brief = NewGeneratingBrief();
        var items = new[] { NewItem(brief, 1), NewItem(brief, 1) };

        Assert.Throws<ArgumentException>(() => brief.Complete(2, items));
    }

    [Fact]
    public void Complete_rejects_duplicate_technology_updates()
    {
        var brief = NewGeneratingBrief();
        var updateId = Guid.NewGuid();
        var items = new[] { NewItem(brief, 1, updateId), NewItem(brief, 2, updateId) };

        Assert.Throws<ArgumentException>(() => brief.Complete(2, items));
    }

    [Fact]
    public void Complete_rejects_items_from_another_brief()
    {
        var brief = NewGeneratingBrief();
        var other = NewGeneratingBrief();
        var foreignItem = NewItem(other, 1);

        Assert.Throws<ArgumentException>(() => brief.Complete(1, [foreignItem]));
    }

    [Fact]
    public void Brief_item_requires_at_least_one_source()
    {
        var brief = NewGeneratingBrief();

        Assert.Throws<ArgumentException>(() => BriefItem.Create(
            brief.Id,
            Guid.NewGuid(),
            1,
            "Title",
            "Topic",
            "Summary",
            "Why relevant",
            72d,
            NowUtc,
            "generate-1",
            "generate-1",
            []));
    }
}
