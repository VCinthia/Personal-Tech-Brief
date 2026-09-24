using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Analysis;
using PersonalTechBrief.Application.Interests;
using PersonalTechBrief.Application.Messaging;
using PersonalTechBrief.Domain.Ingestion;
using PersonalTechBrief.Domain.Interests;
using PersonalTechBrief.Infrastructure.Analysis;
using PersonalTechBrief.Infrastructure.Intelligence;
using PersonalTechBrief.Infrastructure.Intelligence.Contracts;
using InterestPriority = PersonalTechBrief.Domain.Interests.InterestPriority;

namespace PersonalTechBrief.UnitTests.Analysis;

public sealed class GroupingPipelineServiceTests
{
    private static readonly DateTime UtcNow = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Best_similarity_at_the_merge_threshold_merges_into_the_existing_update()
    {
        var existingUpdateId = Guid.NewGuid();
        var harness = new Harness();
        harness.Repository.Candidates = [new GroupingCandidate(existingUpdateId, "Existing\ntopic")];
        harness.Intelligence.SimilarityResult = Similarity((existingUpdateId, 0.78));

        var outcome = await harness.Service.ProcessAsync(harness.Envelope, CancellationToken.None);

        Assert.Equal(GroupingProcessingOutcome.Grouped, outcome);
        Assert.True(harness.Repository.CommitCalled);
        Assert.Equal(existingUpdateId, harness.Repository.CommittedCommand!.MergeTargetTechnologyUpdateId);
        Assert.Equal(0.78d, harness.Repository.CommittedCommand!.SimilarityScore);
        // Source attribution is retained: the merging item is still linked to the group.
        Assert.Equal(harness.Envelope.SourceItemId, harness.Repository.CommittedCommand!.SourceItemId);
    }

    [Fact]
    public async Task Best_similarity_just_below_the_threshold_creates_a_new_update()
    {
        var existingUpdateId = Guid.NewGuid();
        var harness = new Harness();
        harness.Repository.Candidates = [new GroupingCandidate(existingUpdateId, "Existing\ntopic")];
        harness.Intelligence.SimilarityResult = Similarity((existingUpdateId, 0.77));

        var outcome = await harness.Service.ProcessAsync(harness.Envelope, CancellationToken.None);

        Assert.Equal(GroupingProcessingOutcome.Grouped, outcome);
        Assert.Null(harness.Repository.CommittedCommand!.MergeTargetTechnologyUpdateId);
        Assert.Null(harness.Repository.CommittedCommand!.SimilarityScore);
    }

    [Fact]
    public async Task Highest_of_several_similarities_wins_and_equality_merges()
    {
        var lower = Guid.NewGuid();
        var winner = Guid.NewGuid();
        var harness = new Harness();
        harness.Repository.Candidates =
        [
            new GroupingCandidate(lower, "Lower\ntopic"),
            new GroupingCandidate(winner, "Winner\ntopic"),
        ];
        harness.Intelligence.SimilarityResult = Similarity((lower, 0.50), (winner, 0.78));

        await harness.Service.ProcessAsync(harness.Envelope, CancellationToken.None);

        Assert.Equal(winner, harness.Repository.CommittedCommand!.MergeTargetTechnologyUpdateId);
    }

    [Fact]
    public async Task No_candidates_skips_similarity_and_creates_a_new_update()
    {
        var harness = new Harness();
        harness.Repository.Candidates = [];

        var outcome = await harness.Service.ProcessAsync(harness.Envelope, CancellationToken.None);

        Assert.Equal(GroupingProcessingOutcome.Grouped, outcome);
        Assert.False(harness.Intelligence.SimilarityCalled);
        Assert.Null(harness.Repository.CommittedCommand!.MergeTargetTechnologyUpdateId);
    }

    [Fact]
    public async Task Only_active_positive_interest_matches_are_persisted()
    {
        var harness = new Harness();
        var inactiveInterestId = Guid.NewGuid();
        harness.Intelligence.AnalyzeResult = harness.Analyze(matches:
        [
            new InterestMatch { InterestId = harness.HighInterest.Id, MatchStrength = 0.6 },
            new InterestMatch { InterestId = harness.LowInterest.Id, MatchStrength = 0d },
            new InterestMatch { InterestId = inactiveInterestId, MatchStrength = 0.9 },
        ]);

        await harness.Service.ProcessAsync(harness.Envelope, CancellationToken.None);

        var match = Assert.Single(harness.Repository.CommittedCommand!.InterestMatches);
        Assert.Equal(harness.HighInterest.Id, match.InterestId);
        Assert.Equal(0.6d, match.MatchStrength);
    }

    [Fact]
    public async Task Completed_item_marks_the_receipt_processed()
    {
        var harness = new Harness();

        await harness.Service.ProcessAsync(harness.Envelope, CancellationToken.None);

        Assert.Contains(harness.Envelope.SourceItemId, harness.Inbox.ProcessedSourceItemIds);
    }

    [Fact]
    public async Task Score_is_computed_from_group_signals_and_stored()
    {
        var harness = new Harness();
        harness.Repository.Signals = new GroupScoringSignals(
            UtcNow,
            3,
            [new InterestMatchRecord(harness.HighInterest.Id, 1.0)]);

        await harness.Service.ProcessAsync(harness.Envelope, CancellationToken.None);

        // High@1.0 (60) + recent (20) + high impact (20) + 3 hosts (7) = 107.
        Assert.Equal(107d, harness.Repository.ComputedScore, 5);
    }

    [Fact]
    public async Task Already_grouped_item_is_skipped_without_committing()
    {
        var harness = new Harness();
        harness.Repository.SourceItem = harness.BuildItem(existingUpdateId: Guid.NewGuid());

        var outcome = await harness.Service.ProcessAsync(harness.Envelope, CancellationToken.None);

        Assert.Equal(GroupingProcessingOutcome.SkippedAlreadyHandled, outcome);
        Assert.False(harness.Repository.CommitCalled);
        Assert.Contains(harness.Envelope.SourceItemId, harness.Inbox.ProcessedSourceItemIds);
    }

    [Fact]
    public async Task Terminal_item_is_skipped_without_committing()
    {
        var harness = new Harness();
        harness.Repository.SourceItem = harness.BuildItem(status: SourceItemProcessingStatus.Processed);

        var outcome = await harness.Service.ProcessAsync(harness.Envelope, CancellationToken.None);

        Assert.Equal(GroupingProcessingOutcome.SkippedAlreadyHandled, outcome);
        Assert.False(harness.Repository.CommitCalled);
    }

    [Fact]
    public async Task Missing_item_completes_the_receipt()
    {
        var harness = new Harness();
        harness.Repository.SourceItem = null;

        var outcome = await harness.Service.ProcessAsync(harness.Envelope, CancellationToken.None);

        Assert.Equal(GroupingProcessingOutcome.SkippedItemMissing, outcome);
        Assert.Contains(harness.Envelope.SourceItemId, harness.Inbox.ProcessedSourceItemIds);
    }

    [Fact]
    public async Task Transient_intelligence_failure_leaves_the_receipt_pending()
    {
        var harness = new Harness();
        harness.Intelligence.AnalyzeException = new IntelligenceApiException("unavailable", System.Net.HttpStatusCode.ServiceUnavailable);

        var outcome = await harness.Service.ProcessAsync(harness.Envelope, CancellationToken.None);

        Assert.Equal(GroupingProcessingOutcome.RetryScheduled, outcome);
        Assert.Empty(harness.Inbox.ProcessedSourceItemIds);
        var failure = Assert.Single(harness.Repository.Failures);
        Assert.False(failure.Terminal);
    }

    [Fact]
    public async Task Validation_intelligence_failure_is_terminal_and_completes_the_receipt()
    {
        var harness = new Harness();
        harness.Intelligence.AnalyzeException = new IntelligenceApiException("bad", System.Net.HttpStatusCode.UnprocessableEntity);

        var outcome = await harness.Service.ProcessAsync(harness.Envelope, CancellationToken.None);

        Assert.Equal(GroupingProcessingOutcome.FailedTerminal, outcome);
        Assert.Contains(harness.Envelope.SourceItemId, harness.Inbox.ProcessedSourceItemIds);
        var failure = Assert.Single(harness.Repository.Failures);
        Assert.True(failure.Terminal);
    }

    [Fact]
    public async Task Transient_failure_at_the_retry_budget_escalates_to_terminal()
    {
        var harness = new Harness(maxProcessingAttempts: 3);
        harness.Repository.SourceItem = harness.BuildItem(failureCount: 2);
        harness.Intelligence.AnalyzeException = new IntelligenceApiException("timeout");

        var outcome = await harness.Service.ProcessAsync(harness.Envelope, CancellationToken.None);

        Assert.Equal(GroupingProcessingOutcome.FailedTerminal, outcome);
        Assert.Contains(harness.Envelope.SourceItemId, harness.Inbox.ProcessedSourceItemIds);
        Assert.True(Assert.Single(harness.Repository.Failures).Terminal);
    }

    private static SimilarityResponse Similarity(params (Guid UpdateId, double Similarity)[] results) =>
        new()
        {
            CorrelationId = Guid.NewGuid(),
            AlgorithmVersion = "similarity-1",
            Results = results.Select(result => new SimilarityResult
            {
                UpdateId = result.UpdateId,
                Similarity = result.Similarity,
            }).ToList(),
        };

    private sealed class Harness
    {
        public Harness(int maxProcessingAttempts = 5)
        {
            HighInterest = Interest.Create("Platform engineering", InterestPriority.High, UtcNow);
            LowInterest = Interest.Create("Trivia", InterestPriority.Low, UtcNow);

            Envelope = SourceItemReadyEnvelope.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), UtcNow, null);

            Repository = new FakeGroupingRepository
            {
                SourceItem = BuildItem(),
            };
            Intelligence = new FakeIntelligenceApiClient
            {
                AnalyzeResult = Analyze(),
                SimilarityResult = Similarity(),
            };
            Inbox = new FakeInboxStore();
            Interests = new FakeInterestRepository([HighInterest, LowInterest]);

            var options = Options.Create(new GroupingPipelineOptions { MaxProcessingAttempts = maxProcessingAttempts });
            var scorer = new RelevanceScorer(Options.Create(new RelevanceScoringOptions()));
            Service = new GroupingPipelineService(
                Repository,
                Interests,
                Intelligence,
                Inbox,
                scorer,
                options,
                new FixedTimeProvider(UtcNow),
                NullLogger<GroupingPipelineService>.Instance);
        }

        public Interest HighInterest { get; }

        public Interest LowInterest { get; }

        public SourceItemReadyEnvelope Envelope { get; }

        public FakeGroupingRepository Repository { get; }

        public FakeIntelligenceApiClient Intelligence { get; }

        public FakeInboxStore Inbox { get; }

        public FakeInterestRepository Interests { get; }

        public GroupingPipelineService Service { get; }

        public GroupingSourceItem BuildItem(
            SourceItemProcessingStatus status = SourceItemProcessingStatus.Queued,
            Guid? existingUpdateId = null,
            int failureCount = 0) =>
            new(
                Envelope.SourceItemId,
                Envelope.SourceId,
                "A meaningful update",
                "An excerpt",
                UtcNow.AddHours(-1),
                UtcNow.AddHours(-1),
                status,
                failureCount,
                "news.example.test",
                existingUpdateId);

        public AnalyzeResponse Analyze(IReadOnlyList<InterestMatch>? matches = null) =>
            new()
            {
                CorrelationId = Envelope.CorrelationId,
                SourceItemId = Envelope.SourceItemId,
                AnalyzerVersion = "analyze-1",
                Language = "en",
                Normalized = new NormalizedFeatures { Keywords = ["kubernetes"], EventDescriptors = ["release"] },
                Topics = ["platform"],
                InterestMatches = matches ?? [new InterestMatch { InterestId = HighInterest.Id, MatchStrength = 0.8 }],
                Impact = new ImpactSignal { Level = ImpactLevel.High, Confidence = 0.9 },
            };
    }

    private sealed class FakeGroupingRepository : IGroupingRepository
    {
        public GroupingSourceItem? SourceItem { get; set; }

        public IReadOnlyList<GroupingCandidate> Candidates { get; set; } = [];

        public GroupScoringSignals Signals { get; set; } = new(UtcNow, 1, []);

        public bool CommitCalled { get; private set; }

        public CommitGroupingCommand? CommittedCommand { get; private set; }

        public double ComputedScore { get; private set; }

        public List<(string Code, bool Terminal)> Failures { get; } = [];

        public Task<GroupingSourceItem?> GetSourceItemAsync(Guid sourceItemId, CancellationToken cancellationToken) =>
            Task.FromResult(SourceItem);

        public Task<IReadOnlyList<GroupingCandidate>> FindCandidateUpdatesAsync(
            GroupingCandidateQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(Candidates);

        public Task<GroupingCommitResult> CommitGroupingAsync(
            CommitGroupingCommand command,
            Func<GroupScoringSignals, RelevanceScore> computeScore,
            CancellationToken cancellationToken)
        {
            CommitCalled = true;
            CommittedCommand = command;
            var score = computeScore(Signals);
            ComputedScore = score.Total;
            return Task.FromResult(new GroupingCommitResult(Guid.NewGuid(), Created: true, score.Total));
        }

        public Task<int> RecordProcessingFailureAsync(
            Guid sourceItemId, string failureCode, bool terminal, CancellationToken cancellationToken)
        {
            Failures.Add((failureCode, terminal));
            return Task.FromResult(Failures.Count);
        }
    }

    private sealed class FakeIntelligenceApiClient : IIntelligenceApiClient
    {
        public AnalyzeResponse? AnalyzeResult { get; set; }

        public Exception? AnalyzeException { get; set; }

        public SimilarityResponse? SimilarityResult { get; set; }

        public Exception? SimilarityException { get; set; }

        public bool SimilarityCalled { get; private set; }

        public Task<AnalyzeResponse> AnalyzeAsync(AnalyzeRequest request, CancellationToken cancellationToken)
        {
            if (AnalyzeException is not null)
            {
                throw AnalyzeException;
            }

            return Task.FromResult(AnalyzeResult!);
        }

        public Task<SimilarityResponse> ComputeSimilarityAsync(SimilarityRequest request, CancellationToken cancellationToken)
        {
            SimilarityCalled = true;
            if (SimilarityException is not null)
            {
                throw SimilarityException;
            }

            return Task.FromResult(SimilarityResult!);
        }

        public Task<GenerateResponse> GenerateAsync(GenerateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Generation is not exercised by grouping tests.");
    }

    private sealed class FakeInboxStore : IContentProcessingInboxStore
    {
        public List<Guid> ProcessedSourceItemIds { get; } = [];

        public Task<ContentProcessingInboxAcceptance> AcceptAsync(
            SourceItemReadyEnvelope envelope, DateTime receivedAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PendingContentProcessingReceipt>> LoadPendingAsync(
            int batchSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task MarkProcessedAsync(Guid sourceItemId, CancellationToken cancellationToken)
        {
            ProcessedSourceItemIds.Add(sourceItemId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeInterestRepository(IReadOnlyList<Interest> active) : IInterestRepository
    {
        public Task<IReadOnlyList<Interest>> ListAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Interest>> ListActiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(active);

        public Task<Interest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Interest?> GetActiveByNormalizedNameAsync(
            string normalizedName, Guid? excludingId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Add(Interest interest) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        private readonly DateTimeOffset now = new(utcNow, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;
    }
}
