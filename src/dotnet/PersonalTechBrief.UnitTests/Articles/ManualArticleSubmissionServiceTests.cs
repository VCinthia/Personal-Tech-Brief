using Microsoft.Extensions.Logging.Abstractions;
using PersonalTechBrief.Application.Articles;
using PersonalTechBrief.Domain.Ingestion;
using PersonalTechBrief.Infrastructure.Articles;

namespace PersonalTechBrief.UnitTests.Articles;

public sealed class ManualArticleSubmissionServiceTests
{
    private static readonly DateTime Now = new(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("/relative/path")]
    [InlineData("ftp://example.com/file")]
    [InlineData("http://localhost/post")]
    [InlineData("https://LOCALHOST/post")]
    [InlineData("http://127.0.0.1/post")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://10.0.0.1/post")]
    [InlineData("http://[::1]/post")]
    public async Task Rejects_an_invalid_or_reserved_url_without_fetching(string? url)
    {
        var fetcher = new FakeFetcher();
        var service = Create(fetcher: fetcher);

        var result = await service.SubmitAsync(url, CancellationToken.None);

        Assert.Equal(ManualArticleSubmissionOutcome.InvalidUrl, result.Outcome);
        Assert.False(fetcher.WasCalled);
    }

    [Fact]
    public async Task Reports_unfetchable_when_the_fetch_fails()
    {
        var service = Create(fetcher: new FakeFetcher(ArticleFetchResult.Failed("blocked")));

        var result = await service.SubmitAsync("https://example.com/post", CancellationToken.None);

        Assert.Equal(ManualArticleSubmissionOutcome.Unfetchable, result.Outcome);
    }

    [Fact]
    public async Task Reports_not_extractable_when_no_content_is_found()
    {
        var service = Create(
            fetcher: new FakeFetcher(ArticleFetchResult.Success("<html></html>", "https://example.com/post")),
            extractor: new FakeExtractor(null));

        var result = await service.SubmitAsync("https://example.com/post", CancellationToken.None);

        Assert.Equal(ManualArticleSubmissionOutcome.NotExtractable, result.Outcome);
    }

    [Fact]
    public async Task Persists_and_queues_a_new_article_with_the_final_url_and_a_content_hash()
    {
        var store = new FakeStore(PersistManualArticleResult.Persisted(Guid.NewGuid(), Guid.NewGuid()));
        var service = Create(
            fetcher: new FakeFetcher(ArticleFetchResult.Success("<html>ok</html>", "https://example.com/canonical")),
            extractor: new FakeExtractor(new ArticleExtraction("Title", "Body text.")),
            store: store);

        var result = await service.SubmitAsync("https://example.com/post", CancellationToken.None);

        Assert.Equal(ManualArticleSubmissionOutcome.Queued, result.Outcome);
        Assert.NotNull(result.SourceItemId);
        Assert.NotNull(store.LastCommand);
        Assert.Equal("https://example.com/canonical", store.LastCommand!.ArticleUrl);
        Assert.Equal("Title", store.LastCommand.Title);
        Assert.Equal("Body text.", store.LastCommand.Excerpt);
        Assert.False(string.IsNullOrWhiteSpace(store.LastCommand.ContentHash));
        Assert.NotEqual(Guid.Empty, store.LastCommand.CorrelationId);
        Assert.Equal(Now, store.LastCommand.RetrievedAtUtc);
    }

    [Fact]
    public async Task Reports_a_duplicate_with_the_existing_identifier()
    {
        var existing = Guid.NewGuid();
        var service = Create(
            fetcher: new FakeFetcher(ArticleFetchResult.Success("<html>ok</html>", "https://example.com/post")),
            extractor: new FakeExtractor(new ArticleExtraction("Title", "Body.")),
            store: new FakeStore(PersistManualArticleResult.Duplicate(existing)));

        var result = await service.SubmitAsync("https://example.com/post", CancellationToken.None);

        Assert.Equal(ManualArticleSubmissionOutcome.Duplicate, result.Outcome);
        Assert.Equal(existing, result.SourceItemId);
    }

    [Fact]
    public async Task Bounds_the_title_and_excerpt_before_persisting()
    {
        var store = new FakeStore(PersistManualArticleResult.Persisted(Guid.NewGuid(), Guid.NewGuid()));
        var service = Create(
            fetcher: new FakeFetcher(ArticleFetchResult.Success("<html>ok</html>", "https://example.com/post")),
            extractor: new FakeExtractor(new ArticleExtraction(new string('T', 2000), new string('B', 20000))),
            store: store);

        await service.SubmitAsync("https://example.com/post", CancellationToken.None);

        Assert.True(store.LastCommand!.Title.Length <= SourceItemText.TitleMaxLength);
        Assert.True(store.LastCommand.Excerpt!.Length <= SourceItemText.ExcerptMaxLength);
    }

    private static ManualArticleSubmissionService Create(
        FakeFetcher? fetcher = null,
        FakeExtractor? extractor = null,
        FakeStore? store = null) =>
        new(
            fetcher ?? new FakeFetcher(),
            extractor ?? new FakeExtractor(new ArticleExtraction("Title", "Body.")),
            store ?? new FakeStore(PersistManualArticleResult.Persisted(Guid.NewGuid(), Guid.NewGuid())),
            new StubTimeProvider(Now),
            NullLogger<ManualArticleSubmissionService>.Instance);

    private sealed class FakeFetcher(ArticleFetchResult? result = null) : IArticleContentFetcher
    {
        public bool WasCalled { get; private set; }

        public Task<ArticleFetchResult> FetchAsync(Uri url, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(result ?? ArticleFetchResult.Success("<html>ok</html>", url.ToString()));
        }
    }

    private sealed class FakeExtractor(ArticleExtraction? extraction) : IArticleTextExtractor
    {
        public ArticleExtraction? Extract(string html) => extraction;
    }

    private sealed class FakeStore(PersistManualArticleResult result) : IManualArticleStore
    {
        public PersistManualArticleCommand? LastCommand { get; private set; }

        public Task<PersistManualArticleResult> PersistIfNewAsync(PersistManualArticleCommand command, CancellationToken cancellationToken)
        {
            LastCommand = command;
            return Task.FromResult(result);
        }
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
