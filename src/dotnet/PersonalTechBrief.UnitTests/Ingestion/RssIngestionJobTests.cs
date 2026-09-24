using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Ingestion;
using PersonalTechBrief.Application.Sources;
using PersonalTechBrief.Domain.Ingestion;
using PersonalTechBrief.Domain.Sources;
using PersonalTechBrief.Ingestion;
using PersonalTechBrief.Ingestion.Feeds;
using PersonalTechBrief.Infrastructure.Feeds;

namespace PersonalTechBrief.UnitTests.Ingestion;

public sealed class RssIngestionJobTests
{
    private static readonly IPAddress PublicAddress = IPAddress.Parse("8.8.8.8");

    [Fact]
    public async Task Retrieves_fixture_rss_with_bounded_normalized_entries_and_conditionals()
    {
        var source = CreateSource();
        var previousFetch = new DateTime(2026, 9, 13, 10, 0, 0, DateTimeKind.Utc);
        source.RecordIngestionOutcome(
            IngestionRunStatus.Succeeded,
            previousFetch,
            "\"fixture-etag\"",
            new DateTimeOffset(previousFetch));
        using var context = CreateRetrievalContext(_ => XmlResponse(ReadFixture("sample-rss.xml")));

        var result = await context.Client.RetrieveAsync(source, CancellationToken.None);

        Assert.False(result.IsNotModified);
        Assert.Equal(2, result.Items.Count);
        var first = result.Items[0];
        Assert.Equal("rss-fixture-1", first.ExternalId);
        Assert.Equal("A durable first update", first.Title);
        Assert.Equal("A concise fixture excerpt.", first.Excerpt);
        Assert.Equal("https://news.example.test/updates/one?Ref=RSS", first.SourceUrl);
        Assert.Equal(new DateTime(2026, 9, 13, 15, 0, 0, DateTimeKind.Utc), first.PublishedAtUtc);
        Assert.NotNull(first.ContentHash);
        Assert.Equal("https://feeds.example.test/updates/two", result.Items[1].SourceUrl);
        Assert.Equal("\"fixture-etag\"", context.Handler.RequestHeaders.Single(headers => headers.Name == "If-None-Match").Value);
        Assert.Contains(
            context.Handler.RequestHeaders,
            header => header.Name == "If-Modified-Since");
        Assert.Equal(PublicAddress, Assert.Single(context.Factory.ValidatedAddresses));
    }

    [Fact]
    public async Task Returns_not_modified_without_parsing_or_persisting_items()
    {
        var source = CreateSource();
        using var context = CreateRetrievalContext(_ => new HttpResponseMessage(HttpStatusCode.NotModified));

        var result = await context.Client.RetrieveAsync(source, CancellationToken.None);

        Assert.True(result.IsNotModified);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Retrieves_supported_atom_fixture_and_resolves_relative_entry_link()
    {
        using var context = CreateRetrievalContext(_ => XmlResponse(ReadFixture("sample-atom.xml"), "application/atom+xml"));

        var result = await context.Client.RetrieveAsync(CreateSource(), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("atom-fixture-1", item.ExternalId);
        Assert.Equal("Atom fixture update", item.Title);
        Assert.Equal("https://feeds.example.test/atom/entry-1", item.SourceUrl);
        Assert.Equal(new DateTime(2026, 9, 13, 17, 45, 0, DateTimeKind.Utc), item.PublishedAtUtc);
    }

    [Fact]
    public async Task Retries_only_transient_http_failures_and_rejects_malformed_feed()
    {
        var requestCount = 0;
        using var retryContext = CreateRetrievalContext(_ =>
        {
            requestCount++;
            return requestCount == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : XmlResponse(ReadFixture("sample-rss.xml"));
        }, maximumAttempts: 2);

        var recovered = await retryContext.Client.RetrieveAsync(CreateSource(), CancellationToken.None);

        Assert.Equal(2, requestCount);
        Assert.Equal(2, recovered.Items.Count);

        using var malformedContext = CreateRetrievalContext(_ => XmlResponse("<not-a-feed />"));
        var failure = await Assert.ThrowsAsync<FeedRetrievalException>(
            () => malformedContext.Client.RetrieveAsync(CreateSource(), CancellationToken.None));

        Assert.Equal("malformed_feed", failure.ErrorCode);
    }

    [Fact]
    public async Task Rejects_any_unsafe_dns_answer_before_sending_a_request()
    {
        using var context = CreateRetrievalContext(
            _ => XmlResponse(ReadFixture("sample-rss.xml")),
            maximumAttempts: 1,
            addresses: [PublicAddress, IPAddress.Parse("10.0.0.7")]);

        var failure = await Assert.ThrowsAsync<FeedRetrievalException>(
            () => context.Client.RetrieveAsync(CreateSource(), CancellationToken.None));

        Assert.Equal("unsafe_feed_host", failure.ErrorCode);
        Assert.Empty(context.Handler.RequestHeaders);
    }

    [Fact]
    public async Task One_failed_source_does_not_stop_another_source_and_records_each_run_outcome()
    {
        var failedSource = CreateSource("Failed source", "https://failed.example.test/rss.xml");
        var successfulSource = CreateSource("Successful source", "https://successful.example.test/rss.xml");
        var runs = new RecordingRunService();
        var persistence = new RecordingPersistenceService();
        var retrieval = new DelegateFeedRetrievalClient(source => source.Id == failedSource.Id
            ? throw new FeedRetrievalException("network_failure", "The feed request failed.", isTransient: true)
            : new FeedRetrievalResult(
                false,
                [CreateItem("success-1")],
                "\"successful\"",
                null));
        var job = CreateJob([failedSource, successfulSource], runs, persistence, retrieval);

        var result = await job.RunOnceAsync(CancellationToken.None);

        Assert.Equal(2, result.EnabledSourceCount);
        Assert.Equal(1, result.SucceededSourceCount);
        Assert.Equal(1, result.FailedSourceCount);
        Assert.Equal(1, result.NewItemCount);
        Assert.Equal(2, runs.Started.Count);
        Assert.Single(runs.Failures);
        Assert.Equal("network_failure", runs.Failures[0].ErrorCode);
        Assert.Single(runs.Completions);
        Assert.Equal(1, runs.Completions[0].NewItemCount);
        Assert.Single(persistence.Commands);
    }

    [Fact]
    public async Task Repeated_cycles_do_not_count_or_persist_duplicate_feed_entries_again()
    {
        var source = CreateSource();
        var runs = new RecordingRunService();
        var persistence = new RecordingPersistenceService();
        var retrieval = new DelegateFeedRetrievalClient(_ => new FeedRetrievalResult(
            false,
            [CreateItem("stable-id")],
            "\"stable\"",
            null));
        var job = CreateJob([source], runs, persistence, retrieval);

        var first = await job.RunOnceAsync(CancellationToken.None);
        var second = await job.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, first.NewItemCount);
        Assert.Equal(0, second.NewItemCount);
        Assert.Equal(2, persistence.Commands.Count);
        Assert.Equal(2, runs.Completions.Count);
        Assert.Equal(0, runs.Completions[1].NewItemCount);
    }

    [Fact]
    public async Task One_failed_feed_item_does_not_stop_others_and_fails_the_run_without_advancing_validators()
    {
        var source = CreateSource();
        var runs = new RecordingRunService();
        var persistence = new RecordingPersistenceService(throwOnExternalId: "boom");
        var retrieval = new DelegateFeedRetrievalClient(_ => new FeedRetrievalResult(
            false,
            [CreateItem("healthy-1"), CreateItem("boom"), CreateItem("healthy-2")],
            "\"new-etag\"",
            new DateTimeOffset(new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc))));
        var job = CreateJob([source], runs, persistence, retrieval);

        var result = await job.RunOnceAsync(CancellationToken.None);

        // The healthy items on both sides of the failing one were still attempted and counted.
        Assert.Equal(3, persistence.Commands.Count);
        Assert.Equal(2, result.NewItemCount);
        // The source run is failed, and completion — the only path that advances ETag/Last-Modified — never ran.
        Assert.Equal(1, result.EnabledSourceCount);
        Assert.Equal(0, result.SucceededSourceCount);
        Assert.Equal(1, result.FailedSourceCount);
        Assert.Empty(runs.Completions);
        var failure = Assert.Single(runs.Failures);
        Assert.Equal("feed_items_failed", failure.ErrorCode);
        Assert.Equal(3, failure.RetrievedItemCount);
        Assert.Equal(2, failure.NewItemCount);
        // A failed run returns a nonzero process exit code.
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task Empty_enabled_source_list_is_a_successful_no_op()
    {
        var job = CreateJob([], new RecordingRunService(), new RecordingPersistenceService(), NeverCalledRetrieval());

        var result = await job.RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, result.EnabledSourceCount);
        Assert.Equal(0, result.SucceededSourceCount);
        Assert.Equal(0, result.FailedSourceCount);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task All_attempted_sources_failing_yields_a_nonzero_exit_code()
    {
        var source = CreateSource();
        var retrieval = new DelegateFeedRetrievalClient(_ =>
            throw new FeedRetrievalException("network_failure", "The feed request failed.", isTransient: true));
        var job = CreateJob([source], new RecordingRunService(), new RecordingPersistenceService(), retrieval);

        var result = await job.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, result.EnabledSourceCount);
        Assert.Equal(0, result.SucceededSourceCount);
        Assert.Equal(1, result.FailedSourceCount);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task Failure_recording_stays_within_domain_invariants_even_if_the_clock_regresses()
    {
        var source = CreateSource();
        var runs = new RecordingRunService();
        var retrieval = new DelegateFeedRetrievalClient(_ =>
            throw new FeedRetrievalException("transient_http_status", "The feed host rate-limited the request.", isTransient: true));
        // Observed with rate-limited feeds: the wall clock can regress between run start and failure
        // recording. The recorded failure must still satisfy the domain invariants (completion not
        // before start; 0 <= new <= retrieved) so the run is finalized instead of left running.
        var clock = new DecreasingTimeProvider(new DateTime(2026, 9, 17, 12, 0, 30, DateTimeKind.Utc));
        var job = new IngestionJob(
            new RecordingSourceRepository([source]),
            runs,
            new RecordingPersistenceService(),
            retrieval,
            clock,
            NullLogger<IngestionJob>.Instance);

        var result = await job.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, result.FailedSourceCount);
        var started = Assert.Single(runs.Started);
        var failure = Assert.Single(runs.Failures);
        Assert.Equal("transient_http_status", failure.ErrorCode);
        Assert.True(failure.CompletedAtUtc >= started.StartedAtUtc, "completion must not precede the run start");
        Assert.InRange(failure.NewItemCount, 0, failure.RetrievedItemCount);
    }

    private static DelegateFeedRetrievalClient NeverCalledRetrieval() =>
        new(_ => throw new InvalidOperationException("Retrieval must not run when there are no enabled sources."));

    private static IngestionJob CreateJob(
        IReadOnlyList<Source> sources,
        RecordingRunService runs,
        RecordingPersistenceService persistence,
        IFeedRetrievalClient retrieval) =>
        new(
            new RecordingSourceRepository(sources),
            runs,
            persistence,
            retrieval,
            TimeProvider.System,
            NullLogger<IngestionJob>.Instance);

    private static Source CreateSource(
        string name = "Fixture source",
        string feedUrl = "https://feeds.example.test/rss.xml") =>
        Source.Create(name, feedUrl, new DateTime(2026, 9, 13, 9, 0, 0, DateTimeKind.Utc));

    private static RetrievedFeedItem CreateItem(string externalId) =>
        new(
            externalId,
            "https://news.example.test/article",
            "A deterministic item",
            "Fixture excerpt",
            new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc),
            "A1");

    private static RetrievalContext CreateRetrievalContext(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory,
        int maximumAttempts = 3,
        IReadOnlyList<IPAddress>? addresses = null)
    {
        var handler = new RecordingHandler(responseFactory);
        var factory = new RecordingHttpClientFactory(handler);
        var client = new RssAtomFeedRetrievalClient(
            new StaticResolver(addresses ?? [PublicAddress]),
            factory,
            Options.Create(new FeedRetrievalOptions
            {
                MaximumAttempts = maximumAttempts,
                RetryDelayMilliseconds = 0,
            }));
        return new RetrievalContext(client, factory, handler);
    }

    private static HttpResponseMessage XmlResponse(string value, string mediaType = "application/rss+xml") =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(value, Encoding.UTF8, mediaType),
        };

    private static string ReadFixture(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Ingestion", "Fixtures", fileName));

    private sealed class RetrievalContext(
        RssAtomFeedRetrievalClient client,
        RecordingHttpClientFactory factory,
        RecordingHandler handler) : IDisposable
    {
        public RssAtomFeedRetrievalClient Client { get; } = client;

        public RecordingHttpClientFactory Factory { get; } = factory;

        public RecordingHandler Handler { get; } = handler;

        public void Dispose() => Handler.Dispose();
    }

    // Returns a strictly decreasing time on each call, so a later call (failure recording) is earlier
    // than an earlier call (run start) — the clock-regression edge the failure recorder must tolerate.
    private sealed class DecreasingTimeProvider(DateTime start) : TimeProvider
    {
        private int index;

        public override DateTimeOffset GetUtcNow() => new(start.AddSeconds(-index++), TimeSpan.Zero);
    }

    private sealed class StaticResolver(IReadOnlyList<IPAddress> addresses) : IFeedHostAddressResolver
    {
        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            Task.FromResult(addresses);
    }

    private sealed class RecordingHttpClientFactory(HttpMessageHandler handler) : IFeedValidationHttpClientFactory
    {
        public List<IPAddress> ValidatedAddresses { get; } = [];

        public HttpClient Create(Uri feedUrl, IPAddress validatedAddress)
        {
            ValidatedAddresses.Add(validatedAddress);
            var client = new HttpClient(handler, disposeHandler: false)
            {
                Timeout = Timeout.InfiniteTimeSpan,
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalTechBrief/1.0");
            return client;
        }
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<(string Name, string Value)> RequestHeaders { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestHeaders.Clear();
            RequestHeaders.AddRange(request.Headers.SelectMany(header => header.Value.Select(value => (header.Key, value))));
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class DelegateFeedRetrievalClient(Func<Source, FeedRetrievalResult> retrieve) : IFeedRetrievalClient
    {
        public Task<FeedRetrievalResult> RetrieveAsync(Source source, CancellationToken cancellationToken) =>
            Task.FromResult(retrieve(source));
    }

    private sealed class RecordingSourceRepository(IReadOnlyList<Source> sources) : ISourceRepository
    {
        public Task<IReadOnlyList<Source>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(sources);

        public Task<IReadOnlyList<Source>> ListEnabledAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Source>>(sources.Where(source => source.IsEnabled).ToArray());

        public Task<Source?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(sources.SingleOrDefault(source => source.Id == id));

        public Task<Source?> GetEnabledByNormalizedFeedUrlAsync(
            string normalizedFeedUrl,
            Guid? excludingId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Source?>(null);

        public void Add(Source source) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingRunService : IIngestionRunService
    {
        public List<StartIngestionRunCommand> Started { get; } = [];

        public List<CompleteIngestionRunCommand> Completions { get; } = [];

        public List<FailIngestionRunCommand> Failures { get; } = [];

        public Task<StartedIngestionRun> StartAsync(StartIngestionRunCommand command, CancellationToken cancellationToken)
        {
            Started.Add(command);
            return Task.FromResult(new StartedIngestionRun(Guid.NewGuid(), command.SourceId, command.CorrelationId, command.StartedAtUtc));
        }

        public Task CompleteAsync(CompleteIngestionRunCommand command, CancellationToken cancellationToken)
        {
            Completions.Add(command);
            return Task.CompletedTask;
        }

        public Task FailAsync(FailIngestionRunCommand command, CancellationToken cancellationToken)
        {
            Failures.Add(command);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPersistenceService(string? throwOnExternalId = null) : ISourceItemPersistenceService
    {
        private readonly HashSet<string?> persistedExternalIds = [];

        public List<PersistSourceItemCommand> Commands { get; } = [];

        public Task<PersistSourceItemResult> PersistIfNewAsync(
            PersistSourceItemCommand command,
            CancellationToken cancellationToken)
        {
            Commands.Add(command);
            if (throwOnExternalId is not null && command.ExternalId == throwOnExternalId)
            {
                throw new InvalidOperationException($"Synthetic persistence failure for {command.ExternalId}.");
            }

            return Task.FromResult(persistedExternalIds.Add(command.ExternalId)
                ? PersistSourceItemResult.Persisted(Guid.NewGuid(), Guid.NewGuid())
                : PersistSourceItemResult.Duplicate());
        }
    }
}
