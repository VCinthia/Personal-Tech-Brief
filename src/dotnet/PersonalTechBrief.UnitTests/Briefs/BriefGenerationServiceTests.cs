using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Analysis;
using PersonalTechBrief.Application.Briefs;
using PersonalTechBrief.Domain.Briefs;
using PersonalTechBrief.Infrastructure.Briefs;
using PersonalTechBrief.Infrastructure.Intelligence;
using PersonalTechBrief.Infrastructure.Intelligence.Contracts;

namespace PersonalTechBrief.UnitTests.Briefs;

public class BriefGenerationServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UpdateA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UpdateB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Generates_snapshots_for_every_selected_candidate_on_success()
    {
        var brief = NewBrief();
        var repository = new FakeBriefRepository(brief);
        repository.Candidates.Add(new BriefCandidate(UpdateA, 90d, NowUtc.AddHours(-1), AlreadyBriefed: false));
        repository.Candidates.Add(new BriefCandidate(UpdateB, 70d, NowUtc.AddHours(-1), AlreadyBriefed: false));
        repository.Inputs[UpdateA] = Input(UpdateA);
        repository.Inputs[UpdateB] = Input(UpdateB);

        var client = new FakeGenerationClient();
        client.Succeed(UpdateA);
        client.Succeed(UpdateB);

        await CreateService(repository, client).GenerateAsync(brief.Id, CancellationToken.None);

        Assert.Equal(BriefStatus.Completed, brief.Status);
        Assert.Equal(2, brief.CandidateCount);
        Assert.Equal(2, brief.SelectedCount);
        Assert.Equal([1, 2], brief.Items.Select(item => item.Rank));
        // Ranked by score descending: UpdateA (90) before UpdateB (70).
        Assert.Equal(UpdateA, brief.Items[0].TechnologyUpdateId);
        Assert.Equal("Topic-A", brief.Items[0].TopicSnapshot);
        Assert.Equal(90d, brief.Items[0].RelevanceScoreSnapshot);
        Assert.StartsWith("Title:", brief.Items[0].TitleSnapshot);
        Assert.Single(brief.Items[0].Sources);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task Clamps_and_caps_the_generate_request_to_the_frozen_contract_bounds()
    {
        var brief = NewBrief();
        var repository = new FakeBriefRepository(brief);
        repository.Candidates.Add(new BriefCandidate(UpdateA, 90d, NowUtc.AddHours(-1), AlreadyBriefed: false));

        // Domain values that exceed every frozen contract bound.
        var sources = Enumerable.Range(0, 60)
            .Select(_ => new BriefSourceInput(
                Guid.NewGuid(),
                new string('t', 900),
                new string('e', 5000),
                "https://example.test/x",
                NowUtc.AddHours(-2)))
            .ToList();
        var interests = Enumerable.Range(0, 120)
            .Select(_ => new BriefInterestInput(Guid.NewGuid(), new string('n', 300), Domain.Interests.InterestPriority.High))
            .ToList();
        repository.Inputs[UpdateA] = new BriefGenerationInput(UpdateA, new string('p', 400), sources, interests);

        var client = new FakeGenerationClient();
        client.Succeed(UpdateA);

        await CreateService(repository, client).GenerateAsync(brief.Id, CancellationToken.None);

        var request = client.RequestFor(UpdateA);
        Assert.Equal(GenerateContractLimits.PrimaryTopicMaxLength, request.Update.PrimaryTopic.Length);
        Assert.Equal(GenerateContractLimits.MaxSources, request.Update.Sources.Count);
        Assert.All(request.Update.Sources, source => Assert.Equal(GenerateContractLimits.SourceTitleMaxLength, source.Title.Length));
        Assert.All(request.Update.Sources, source => Assert.Equal(GenerateContractLimits.SourceExcerptMaxLength, source.Excerpt!.Length));
        Assert.Equal(GenerateContractLimits.MaxMatchedInterests, request.MatchedInterests.Count);
        Assert.All(request.MatchedInterests, interest => Assert.Equal(GenerateContractLimits.InterestNameMaxLength, interest.Name.Length));
    }

    [Fact]
    public async Task Excludes_a_candidate_whose_generation_fails_persistently()
    {
        var brief = NewBrief();
        var repository = new FakeBriefRepository(brief);
        repository.Candidates.Add(new BriefCandidate(UpdateA, 90d, NowUtc.AddHours(-1), AlreadyBriefed: false));
        repository.Candidates.Add(new BriefCandidate(UpdateB, 70d, NowUtc.AddHours(-1), AlreadyBriefed: false));
        repository.Inputs[UpdateA] = Input(UpdateA);
        repository.Inputs[UpdateB] = Input(UpdateB);

        var client = new FakeGenerationClient();
        client.Succeed(UpdateA);
        client.FailPersistently(UpdateB, HttpStatusCode.BadGateway);

        await CreateService(repository, client).GenerateAsync(brief.Id, CancellationToken.None);

        Assert.Equal(BriefStatus.Completed, brief.Status);
        Assert.Equal(2, brief.CandidateCount);
        Assert.Equal(1, brief.SelectedCount);
        Assert.Equal(UpdateA, Assert.Single(brief.Items).TechnologyUpdateId);
        // The excluded candidate exhausted its bounded transient-retry budget (3 attempts).
        Assert.Equal(3, client.AttemptsFor(UpdateB));
    }

    [Fact]
    public async Task Does_not_retry_a_non_transient_generation_failure()
    {
        var brief = NewBrief();
        var repository = new FakeBriefRepository(brief);
        repository.Candidates.Add(new BriefCandidate(UpdateA, 90d, NowUtc.AddHours(-1), AlreadyBriefed: false));
        repository.Inputs[UpdateA] = Input(UpdateA);

        var client = new FakeGenerationClient();
        client.FailPersistently(UpdateA, HttpStatusCode.BadRequest);

        await CreateService(repository, client).GenerateAsync(brief.Id, CancellationToken.None);

        Assert.Equal(BriefStatus.Completed, brief.Status);
        Assert.Empty(brief.Items);
        Assert.Equal(1, client.AttemptsFor(UpdateA));
    }

    [Fact]
    public async Task Retries_a_transient_failure_then_succeeds()
    {
        var brief = NewBrief();
        var repository = new FakeBriefRepository(brief);
        repository.Candidates.Add(new BriefCandidate(UpdateA, 90d, NowUtc.AddHours(-1), AlreadyBriefed: false));
        repository.Inputs[UpdateA] = Input(UpdateA);

        var client = new FakeGenerationClient();
        client.FailTransientlyThenSucceed(UpdateA, transientAttempts: 2);

        await CreateService(repository, client).GenerateAsync(brief.Id, CancellationToken.None);

        Assert.Equal(BriefStatus.Completed, brief.Status);
        Assert.Equal(UpdateA, Assert.Single(brief.Items).TechnologyUpdateId);
        Assert.Equal(3, client.AttemptsFor(UpdateA));
    }

    [Fact]
    public async Task All_candidates_failing_yields_an_empty_completed_brief()
    {
        var brief = NewBrief();
        var repository = new FakeBriefRepository(brief);
        repository.Candidates.Add(new BriefCandidate(UpdateA, 90d, NowUtc.AddHours(-1), AlreadyBriefed: false));
        repository.Candidates.Add(new BriefCandidate(UpdateB, 70d, NowUtc.AddHours(-1), AlreadyBriefed: false));
        repository.Inputs[UpdateA] = Input(UpdateA);
        repository.Inputs[UpdateB] = Input(UpdateB);

        var client = new FakeGenerationClient();
        client.FailPersistently(UpdateA, HttpStatusCode.GatewayTimeout);
        client.FailPersistently(UpdateB, HttpStatusCode.ServiceUnavailable);

        await CreateService(repository, client).GenerateAsync(brief.Id, CancellationToken.None);

        Assert.Equal(BriefStatus.Completed, brief.Status);
        Assert.Equal(2, brief.CandidateCount);
        Assert.Equal(0, brief.SelectedCount);
        Assert.Empty(brief.Items);
    }

    [Fact]
    public async Task Marks_the_brief_failed_when_candidate_loading_throws()
    {
        var brief = NewBrief();
        var repository = new FakeBriefRepository(brief) { ThrowOnLoadCandidates = true };

        await CreateService(repository, new FakeGenerationClient()).GenerateAsync(brief.Id, CancellationToken.None);

        Assert.Equal(BriefStatus.Failed, brief.Status);
    }

    [Fact]
    public async Task An_already_completed_brief_is_never_rewritten()
    {
        var brief = NewBrief();
        brief.Complete(1, []);
        var repository = new FakeBriefRepository(brief);

        await CreateService(repository, new FakeGenerationClient()).GenerateAsync(brief.Id, CancellationToken.None);

        Assert.Equal(BriefStatus.Completed, brief.Status);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    private static Brief NewBrief() =>
        Brief.Create(NowUtc, NowUtc.AddDays(-7), NowUtc, "brief-1", Guid.NewGuid());

    private static BriefGenerationInput Input(Guid updateId)
    {
        var suffix = updateId == UpdateA ? "A" : "B";
        return new BriefGenerationInput(
            updateId,
            $"Topic-{suffix}",
            [new BriefSourceInput(Guid.NewGuid(), $"Source-{suffix}", "excerpt", "https://example.test/x", NowUtc.AddHours(-2))],
            [new BriefInterestInput(Guid.NewGuid(), $"Interest-{suffix}", Domain.Interests.InterestPriority.High)]);
    }

    private static BriefGenerationService CreateService(FakeBriefRepository repository, FakeGenerationClient client) =>
        new(
            repository,
            new BriefCandidateSelector(),
            client,
            Options.Create(new RelevanceScoringOptions { SelectionThreshold = 55d }),
            Options.Create(new BriefGenerationOptions { MaxItems = 5, MaxGenerationAttempts = 3, RetryBaseDelayMilliseconds = 0 }),
            new FixedTimeProvider(NowUtc),
            NullLogger<BriefGenerationService>.Instance);

    private sealed class FakeBriefRepository(Brief brief) : IBriefRepository
    {
        public List<BriefCandidate> Candidates { get; } = [];

        public Dictionary<Guid, BriefGenerationInput> Inputs { get; } = [];

        public int SaveChangesCount { get; private set; }

        public bool ThrowOnLoadCandidates { get; init; }

        public Task<DateTime?> GetLatestCompletedBriefGeneratedAtUtcAsync(CancellationToken cancellationToken) =>
            Task.FromResult<DateTime?>(null);

        public Task AddAsync(Brief brief, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Brief?> GetForGenerationAsync(Guid briefId, CancellationToken cancellationToken) =>
            Task.FromResult(briefId == brief.Id ? brief : null);

        public Task<IReadOnlyList<BriefCandidate>> LoadCandidatesAsync(
            DateTime windowStartUtc,
            double threshold,
            CancellationToken cancellationToken)
        {
            if (ThrowOnLoadCandidates)
            {
                throw new InvalidOperationException("candidate loading failed");
            }

            return Task.FromResult<IReadOnlyList<BriefCandidate>>(Candidates);
        }

        public Task<BriefGenerationInput?> LoadGenerationInputAsync(Guid technologyUpdateId, CancellationToken cancellationToken) =>
            Task.FromResult(Inputs.GetValueOrDefault(technologyUpdateId));

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(Guid briefId, CancellationToken cancellationToken)
        {
            if (briefId == brief.Id && brief.Status == BriefStatus.Generating)
            {
                brief.Fail();
            }

            return Task.CompletedTask;
        }

        public Task<int> FailOrphanedGeneratingBriefsAsync(DateTime createdBeforeUtc, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task<BriefReadModel?> GetCurrentAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BriefReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BriefPage> ListAsync(string? cursor, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeGenerationClient : IIntelligenceApiClient
    {
        private readonly Dictionary<Guid, Func<int, GenerateResponse>> _behaviors = [];
        private readonly Dictionary<Guid, int> _attempts = [];
        private readonly Dictionary<Guid, GenerateRequest> _requests = [];

        public GenerateRequest RequestFor(Guid updateId) => _requests[updateId];

        public void Succeed(Guid updateId) => _behaviors[updateId] = _ => Response(updateId);

        public void FailPersistently(Guid updateId, HttpStatusCode statusCode) =>
            _behaviors[updateId] = _ => throw new IntelligenceApiException("failed", statusCode, "title");

        public void FailTransientlyThenSucceed(Guid updateId, int transientAttempts) =>
            _behaviors[updateId] = attempt => attempt <= transientAttempts
                ? throw new IntelligenceApiException("transient", HttpStatusCode.BadGateway, "title")
                : Response(updateId);

        public int AttemptsFor(Guid updateId) => _attempts.GetValueOrDefault(updateId);

        public Task<AnalyzeResponse> AnalyzeAsync(AnalyzeRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SimilarityResponse> ComputeSimilarityAsync(SimilarityRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GenerateResponse> GenerateAsync(GenerateRequest request, CancellationToken cancellationToken)
        {
            var updateId = request.Update.TechnologyUpdateId;
            _requests[updateId] = request;
            var attempt = _attempts.GetValueOrDefault(updateId) + 1;
            _attempts[updateId] = attempt;
            return Task.FromResult(_behaviors[updateId](attempt));
        }

        private static GenerateResponse Response(Guid updateId) => new()
        {
            CorrelationId = Guid.NewGuid(),
            TechnologyUpdateId = updateId,
            GenerationVersion = "generate-1",
            Title = $"Title: {updateId}",
            Summary = "Summary",
            WhyRelevant = "Why relevant",
        };
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        private readonly DateTimeOffset _now = new(utcNow, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
