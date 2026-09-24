using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PersonalTechBrief.Domain.Ingestion;
using PersonalTechBrief.Infrastructure.Articles;
using PersonalTechBrief.Infrastructure.Persistence;
using PersonalTechBrief.IntegrationTests.Interests;

namespace PersonalTechBrief.IntegrationTests.Articles;

public sealed class SqlServerManualArticleApiTests(SqlServerInterestApiFixture fixture)
    : IClassFixture<SqlServerInterestApiFixture>
{
    [Fact]
    public async Task Submitting_a_valid_article_returns_202_and_persists_a_source_less_item()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();

        using var client = CreateClient(
            factory,
            ArticleFetchResult.Success("<html>ok</html>", "https://example.com/post"),
            new ArticleExtraction("Extracted title", "Extracted body."));

        using var response = await client.PostAsJsonAsync("/api/v1/articles", new { url = "https://example.com/post" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ArticleApiResponse>();
        Assert.Equal("queued", body!.Status);
        Assert.NotNull(body.ArticleId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        var item = await dbContext.SourceItems.SingleAsync();
        Assert.Equal(SourceItemOriginType.ManualUrl, item.OriginType);
        Assert.Null(item.SourceId);
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Resubmitting_the_same_article_returns_202_duplicate_without_a_second_item()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();

        using var client = CreateClient(
            factory,
            ArticleFetchResult.Success("<html>ok</html>", "https://example.com/dup"),
            new ArticleExtraction("Title", "Body."));

        using var first = await client.PostAsJsonAsync("/api/v1/articles", new { url = "https://example.com/dup" });
        using var second = await client.PostAsJsonAsync("/api/v1/articles", new { url = "https://example.com/dup" });

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<ArticleApiResponse>();
        Assert.Equal("duplicate", secondBody!.Status);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        Assert.Equal(1, await dbContext.SourceItems.CountAsync());
    }

    [Fact]
    public async Task Rejects_a_malformed_url_with_400()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();

        using var client = CreateClient(factory, ArticleFetchResult.Success("<html/>", "x"), new ArticleExtraction("T", "B"));

        using var response = await client.PostAsJsonAsync("/api/v1/articles", new { url = "not-a-valid-url" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reports_422_when_the_article_cannot_be_fetched()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();

        using var client = CreateClient(factory, ArticleFetchResult.Failed("blocked"), extraction: null);

        using var response = await client.PostAsJsonAsync("/api/v1/articles", new { url = "https://example.com/blocked" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        Assert.Equal(0, await dbContext.SourceItems.CountAsync());
    }

    private static HttpClient CreateClient(
        SqlServerInterestApiFactory factory,
        ArticleFetchResult fetchResult,
        ArticleExtraction? extraction)
    {
        var configured = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IArticleContentFetcher>();
                services.AddSingleton<IArticleContentFetcher>(new StubFetcher(fetchResult));
                services.RemoveAll<IArticleTextExtractor>();
                services.AddSingleton<IArticleTextExtractor>(new StubExtractor(extraction));
            }));
        return configured.CreateClient();
    }

    private sealed record ArticleApiResponse(Guid? ArticleId, string? Status);

    private sealed class StubFetcher(ArticleFetchResult result) : IArticleContentFetcher
    {
        public Task<ArticleFetchResult> FetchAsync(Uri url, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class StubExtractor(ArticleExtraction? extraction) : IArticleTextExtractor
    {
        public ArticleExtraction? Extract(string html) => extraction;
    }
}
