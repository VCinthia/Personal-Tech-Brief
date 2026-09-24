using PersonalTechBrief.Application.Interactions;
using PersonalTechBrief.Domain.Interactions;

namespace PersonalTechBrief.UnitTests.Interactions;

// Unit coverage for the FR-013 interaction subset (UC-009/010, AC-012/013): feedback append + outcomes,
// saved idempotency, and the source-open association guard, all against a fake repository.
public class InteractionServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UpdateId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SourceItemId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BriefItemId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task RecordFeedback_appends_a_row_attributed_to_the_update_and_brief_item()
    {
        var repository = new FakeInteractionRepository();
        repository.Updates.Add(UpdateId);
        repository.BriefItems.Add((BriefItemId, UpdateId));

        var outcome = await Service(repository)
            .RecordFeedbackAsync(UpdateId, FeedbackType.Relevant, BriefItemId, CancellationToken.None);

        Assert.Equal(RecordFeedbackOutcome.Recorded, outcome);
        var feedback = Assert.Single(repository.Feedback);
        Assert.Equal(UpdateId, feedback.TechnologyUpdateId);
        Assert.Equal(BriefItemId, feedback.BriefItemId);
        Assert.Equal(FeedbackType.Relevant, feedback.FeedbackType);
        Assert.Equal(NowUtc, feedback.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, feedback.CreatedAtUtc.Kind);
    }

    [Fact]
    public async Task RecordFeedback_returns_UpdateNotFound_and_records_nothing_when_the_update_is_absent()
    {
        var repository = new FakeInteractionRepository();

        var outcome = await Service(repository)
            .RecordFeedbackAsync(UpdateId, FeedbackType.Relevant, briefItemId: null, CancellationToken.None);

        Assert.Equal(RecordFeedbackOutcome.UpdateNotFound, outcome);
        Assert.Empty(repository.Feedback);
    }

    [Fact]
    public async Task RecordFeedback_returns_BriefItemNotFound_when_the_supplied_brief_item_is_absent()
    {
        var repository = new FakeInteractionRepository();
        repository.Updates.Add(UpdateId);

        var outcome = await Service(repository)
            .RecordFeedbackAsync(UpdateId, FeedbackType.Relevant, BriefItemId, CancellationToken.None);

        Assert.Equal(RecordFeedbackOutcome.BriefItemNotFound, outcome);
        Assert.Empty(repository.Feedback);
    }

    [Fact]
    public async Task RecordFeedback_returns_BriefItemNotFound_when_the_brief_item_belongs_to_another_update()
    {
        var repository = new FakeInteractionRepository();
        var otherUpdateId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        repository.Updates.Add(UpdateId);
        // The brief item exists but is attributed to a different update.
        repository.BriefItems.Add((BriefItemId, otherUpdateId));

        var outcome = await Service(repository)
            .RecordFeedbackAsync(UpdateId, FeedbackType.Relevant, BriefItemId, CancellationToken.None);

        Assert.Equal(RecordFeedbackOutcome.BriefItemNotFound, outcome);
        Assert.Empty(repository.Feedback);
    }

    [Fact]
    public async Task RecordFeedback_appends_a_later_row_so_the_latest_supersedes()
    {
        var repository = new FakeInteractionRepository();
        repository.Updates.Add(UpdateId);
        var service = Service(repository);

        await service.RecordFeedbackAsync(UpdateId, FeedbackType.Relevant, null, CancellationToken.None);
        await service.RecordFeedbackAsync(UpdateId, FeedbackType.NotRelevant, null, CancellationToken.None);

        // Both rows are retained (append-only); the most recent row is the current feedback.
        Assert.Equal(2, repository.Feedback.Count);
        Assert.Equal(FeedbackType.Relevant, repository.Feedback[0].FeedbackType);
        Assert.Equal(FeedbackType.NotRelevant, repository.Feedback[^1].FeedbackType);
    }

    [Fact]
    public async Task Save_is_idempotent_and_records_a_single_row()
    {
        var repository = new FakeInteractionRepository();
        repository.Updates.Add(UpdateId);
        var service = Service(repository);

        Assert.True(await service.SaveAsync(UpdateId, CancellationToken.None));
        Assert.True(await service.SaveAsync(UpdateId, CancellationToken.None));

        Assert.Single(repository.Saved);
    }

    [Fact]
    public async Task Save_returns_false_when_the_update_is_absent()
    {
        var repository = new FakeInteractionRepository();

        Assert.False(await Service(repository).SaveAsync(UpdateId, CancellationToken.None));
        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async Task Unsave_removes_the_saved_row_and_is_a_no_op_when_absent()
    {
        var repository = new FakeInteractionRepository();
        repository.Updates.Add(UpdateId);
        var service = Service(repository);
        await service.SaveAsync(UpdateId, CancellationToken.None);

        await service.UnsaveAsync(UpdateId, CancellationToken.None);
        Assert.Empty(repository.Saved);

        // A second unsave is still a no-op.
        await service.UnsaveAsync(UpdateId, CancellationToken.None);
        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async Task RecordSourceOpen_rejects_a_source_that_does_not_support_the_update()
    {
        var repository = new FakeInteractionRepository();

        var recorded = await Service(repository)
            .RecordSourceOpenAsync(UpdateId, SourceItemId, CancellationToken.None);

        Assert.False(recorded);
        Assert.Empty(repository.SourceOpens);
    }

    [Fact]
    public async Task RecordSourceOpen_records_an_event_for_a_supporting_source()
    {
        var repository = new FakeInteractionRepository();
        repository.SupportingSources.Add((UpdateId, SourceItemId));

        var recorded = await Service(repository)
            .RecordSourceOpenAsync(UpdateId, SourceItemId, CancellationToken.None);

        Assert.True(recorded);
        var sourceOpen = Assert.Single(repository.SourceOpens);
        Assert.Equal(UpdateId, sourceOpen.TechnologyUpdateId);
        Assert.Equal(SourceItemId, sourceOpen.SourceItemId);
        Assert.Equal(NowUtc, sourceOpen.OpenedAtUtc);
    }

    private static InteractionService Service(IInteractionRepository repository) =>
        new(repository, new FixedTimeProvider(NowUtc));

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private sealed class FakeInteractionRepository : IInteractionRepository
    {
        public HashSet<Guid> Updates { get; } = [];

        public HashSet<(Guid BriefItemId, Guid UpdateId)> BriefItems { get; } = [];

        public HashSet<(Guid UpdateId, Guid SourceItemId)> SupportingSources { get; } = [];

        public List<UserFeedback> Feedback { get; } = [];

        public HashSet<Guid> Saved { get; } = [];

        public List<SourceOpenEvent> SourceOpens { get; } = [];

        public Task<bool> UpdateExistsAsync(Guid technologyUpdateId, CancellationToken cancellationToken) =>
            Task.FromResult(Updates.Contains(technologyUpdateId));

        public Task<bool> BriefItemBelongsToUpdateAsync(
            Guid briefItemId,
            Guid technologyUpdateId,
            CancellationToken cancellationToken) =>
            Task.FromResult(BriefItems.Contains((briefItemId, technologyUpdateId)));

        public Task<bool> IsSupportingSourceAsync(Guid technologyUpdateId, Guid sourceItemId, CancellationToken cancellationToken) =>
            Task.FromResult(SupportingSources.Contains((technologyUpdateId, sourceItemId)));

        public Task<bool> IsSavedAsync(Guid technologyUpdateId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.Contains(technologyUpdateId));

        public Task AddFeedbackAsync(UserFeedback feedback, CancellationToken cancellationToken)
        {
            Feedback.Add(feedback);
            return Task.CompletedTask;
        }

        public Task ClearFeedbackAsync(Guid technologyUpdateId, CancellationToken cancellationToken)
        {
            Feedback.RemoveAll(feedback => feedback.TechnologyUpdateId == technologyUpdateId);
            return Task.CompletedTask;
        }

        public Task AddSavedAsync(SavedUpdate saved, CancellationToken cancellationToken)
        {
            Saved.Add(saved.TechnologyUpdateId);
            return Task.CompletedTask;
        }

        public Task RemoveSavedAsync(Guid technologyUpdateId, CancellationToken cancellationToken)
        {
            Saved.Remove(technologyUpdateId);
            return Task.CompletedTask;
        }

        public Task AddSourceOpenAsync(SourceOpenEvent sourceOpen, CancellationToken cancellationToken)
        {
            SourceOpens.Add(sourceOpen);
            return Task.CompletedTask;
        }
    }
}
