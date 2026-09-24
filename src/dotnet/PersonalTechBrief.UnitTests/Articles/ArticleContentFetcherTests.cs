using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Infrastructure.Articles;
using PersonalTechBrief.Infrastructure.Feeds;

namespace PersonalTechBrief.UnitTests.Articles;

public sealed class ArticleContentFetcherTests
{
    private static readonly Uri Article = new("https://example.com/post");
    private static readonly IPAddress PublicAddress = IPAddress.Parse("93.184.216.34");

    [Fact]
    public async Task Rejects_a_localhost_host_without_connecting()
    {
        var requester = new FakeRequester();
        var fetcher = Create(new FakeResolver(), requester);

        var result = await fetcher.FetchAsync(new Uri("http://localhost/post"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(0, requester.CallCount);
    }

    [Fact]
    public async Task Rejects_a_host_that_resolves_to_a_private_address()
    {
        var resolver = new FakeResolver { ["example.com"] = [IPAddress.Parse("10.0.0.5")] };
        var requester = new FakeRequester();
        var fetcher = Create(resolver, requester);

        var result = await fetcher.FetchAsync(Article, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(0, requester.CallCount);
    }

    [Fact]
    public async Task Rejects_a_host_that_does_not_resolve()
    {
        var fetcher = Create(new FakeResolver(), new FakeRequester());

        var result = await fetcher.FetchAsync(Article, CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Returns_the_html_for_a_public_html_response()
    {
        var resolver = new FakeResolver { ["example.com"] = [PublicAddress] };
        var requester = new FakeRequester(Html("<html><body>ok</body></html>"));
        var fetcher = Create(resolver, requester);

        var result = await fetcher.FetchAsync(Article, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains("ok", result.Html);
        Assert.Equal(Article.ToString(), result.FinalUrl);
    }

    [Fact]
    public async Task Rejects_a_non_html_response()
    {
        var resolver = new FakeResolver { ["example.com"] = [PublicAddress] };
        var requester = new FakeRequester(new PinnedArticleResponse(200, null, "application/json", "{}", false));
        var fetcher = Create(resolver, requester);

        var result = await fetcher.FetchAsync(Article, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("the response was not an HTML document", result.FailureReason);
    }

    [Fact]
    public async Task Rejects_a_body_over_the_cap()
    {
        var resolver = new FakeResolver { ["example.com"] = [PublicAddress] };
        var requester = new FakeRequester(new PinnedArticleResponse(200, null, "text/html", null, true));
        var fetcher = Create(resolver, requester);

        var result = await fetcher.FetchAsync(Article, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("the response exceeded the size limit", result.FailureReason);
    }

    [Fact]
    public async Task Rejects_an_unsuccessful_status()
    {
        var resolver = new FakeResolver { ["example.com"] = [PublicAddress] };
        var requester = new FakeRequester(new PinnedArticleResponse(404, null, "text/html", "<html/>", false));
        var fetcher = Create(resolver, requester);

        var result = await fetcher.FetchAsync(Article, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("the article could not be retrieved", result.FailureReason);
    }

    [Fact]
    public async Task Follows_a_redirect_to_another_public_host_and_revalidates_it()
    {
        var resolver = new FakeResolver
        {
            ["example.com"] = [PublicAddress],
            ["cdn.example.net"] = [IPAddress.Parse("151.101.1.1")],
        };
        var requester = new FakeRequester(
            Redirect("https://cdn.example.net/final"),
            Html("<html><body>final</body></html>"));
        var fetcher = Create(resolver, requester);

        var result = await fetcher.FetchAsync(Article, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("https://cdn.example.net/final", result.FinalUrl);
        Assert.Equal(2, requester.CallCount);
        Assert.Contains("cdn.example.net", resolver.ResolvedHosts);
    }

    [Fact]
    public async Task Rejects_a_redirect_to_a_private_host()
    {
        var resolver = new FakeResolver
        {
            ["example.com"] = [PublicAddress],
            ["internal.local"] = [IPAddress.Parse("169.254.169.254")],
        };
        var requester = new FakeRequester(Redirect("http://internal.local/metadata"));
        var fetcher = Create(resolver, requester);

        var result = await fetcher.FetchAsync(Article, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("host resolved to an unsafe address", result.FailureReason);
        Assert.Equal(1, requester.CallCount);
    }

    [Fact]
    public async Task Rejects_a_redirect_that_leaves_the_http_scheme()
    {
        var resolver = new FakeResolver { ["example.com"] = [PublicAddress] };
        var requester = new FakeRequester(Redirect("ftp://example.com/file"));
        var fetcher = Create(resolver, requester);

        var result = await fetcher.FetchAsync(Article, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("redirect left the http/https scheme", result.FailureReason);
    }

    [Fact]
    public async Task Fails_when_the_redirect_budget_is_exhausted()
    {
        var resolver = new FakeResolver { ["example.com"] = [PublicAddress] };
        var requester = new FakeRequester(
            Redirect("https://example.com/a"),
            Redirect("https://example.com/b"));
        var fetcher = Create(resolver, requester, maxRedirects: 1);

        var result = await fetcher.FetchAsync(Article, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("too many redirects", result.FailureReason);
    }

    [Fact]
    public async Task Fails_a_redirect_without_a_location()
    {
        var resolver = new FakeResolver { ["example.com"] = [PublicAddress] };
        var fetcher = Create(resolver, new FakeRequester(new PinnedArticleResponse(302, null, null, null, false)));

        var result = await fetcher.FetchAsync(Article, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("redirect without a location", result.FailureReason);
    }

    [Fact]
    public async Task Maps_a_transport_failure_to_a_failed_result()
    {
        var resolver = new FakeResolver { ["example.com"] = [PublicAddress] };
        var fetcher = Create(resolver, new FakeRequester(new HttpRequestException("boom")));

        var result = await fetcher.FetchAsync(Article, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("the article host could not be reached", result.FailureReason);
    }

    private static ArticleContentFetcher Create(
        FakeResolver resolver,
        FakeRequester requester,
        int maxRedirects = 5) =>
        new(
            resolver,
            requester,
            Options.Create(new ManualArticleFetchOptions
            {
                TimeoutSeconds = 5,
                MaximumResponseBytes = 5_242_880,
                MaxRedirects = maxRedirects,
                UserAgent = "test-agent/1.0",
            }),
            NullLogger<ArticleContentFetcher>.Instance);

    private static PinnedArticleResponse Html(string html) => new(200, null, "text/html", html, false);

    private static PinnedArticleResponse Redirect(string location) => new(302, new Uri(location), null, null, false);

    private sealed class FakeResolver : Dictionary<string, IReadOnlyList<IPAddress>>, IFeedHostAddressResolver
    {
        public List<string> ResolvedHosts { get; } = [];

        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            ResolvedHosts.Add(host);
            return Task.FromResult(TryGetValue(host, out var addresses) ? addresses : []);
        }
    }

    private sealed class FakeRequester(params object[] responses) : IPinnedArticleRequester
    {
        private readonly Queue<object> queued = new(responses);

        public int CallCount { get; private set; }

        public Task<PinnedArticleResponse> SendAsync(
            Uri url,
            IPAddress pinnedAddress,
            string userAgent,
            long maximumBytes,
            CancellationToken cancellationToken)
        {
            CallCount++;
            var next = queued.Count > 0 ? queued.Dequeue() : new PinnedArticleResponse(200, null, "text/html", "<html/>", false);
            if (next is Exception exception)
            {
                throw exception;
            }

            return Task.FromResult((PinnedArticleResponse)next);
        }
    }
}
