using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Ingestion;
using PersonalTechBrief.Application.Sources;
using PersonalTechBrief.Domain.Sources;
using PersonalTechBrief.Ingestion;
using PersonalTechBrief.Ingestion.Feeds;
using PersonalTechBrief.Infrastructure.Feeds;
using PersonalTechBrief.Infrastructure.Persistence;
using PersonalTechBrief.IntegrationTests.Interests;

namespace PersonalTechBrief.IntegrationTests.Ingestion;

public sealed class SqlServerRssIngestionJobTests(SqlServerInterestApiFixture fixture)
    : IClassFixture<SqlServerInterestApiFixture>
{
    [Fact]
    public async Task Committed_sql_migrations_run_fixture_ingestion_idempotently_and_write_transactional_outbox()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var sourceRepository = scope.ServiceProvider.GetRequiredService<ISourceRepository>();
        var source = Source.Create(
            "Job RSS fixture",
            "https://feeds.example.test/job-rss.xml",
            new DateTime(2026, 9, 13, 16, 30, 0, DateTimeKind.Utc));
        sourceRepository.Add(source);
        await sourceRepository.SaveChangesAsync(CancellationToken.None);

        using var handler = new FixtureHandler(ReadFixture());
        var retrieval = new RssAtomFeedRetrievalClient(
            new StaticResolver(),
            new FixtureHttpClientFactory(handler),
            Options.Create(new FeedRetrievalOptions { MaximumAttempts = 1 }));
        var job = new IngestionJob(
            sourceRepository,
            scope.ServiceProvider.GetRequiredService<IIngestionRunService>(),
            scope.ServiceProvider.GetRequiredService<ISourceItemPersistenceService>(),
            retrieval,
            TimeProvider.System,
            NullLogger<IngestionJob>.Instance);

        var first = await job.RunOnceAsync(CancellationToken.None);
        var second = await job.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, first.NewItemCount);
        Assert.Equal(0, second.NewItemCount);
        Assert.Equal(2, handler.RequestCount);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        var item = await dbContext.SourceItems.SingleAsync();
        Assert.Equal("sql-job-entry-1", item.ExternalId);
        Assert.Equal("https://news.example.test/sql/entry-1", item.NormalizedUrl);
        Assert.Equal("SQL-backed fixture entry", item.Title);
        Assert.NotNull(item.ContentHash);
        Assert.Single(await dbContext.OutboxMessages.ToListAsync());
        var runs = await dbContext.IngestionRuns.OrderBy(run => run.StartedAtUtc).ToListAsync();
        Assert.Equal(2, runs.Count);
        Assert.All(runs, run =>
        {
            Assert.Equal("Succeeded", run.Status.ToString());
            Assert.Equal(1, run.RetrievedItemCount);
        });
        Assert.Equal(1, runs[0].NewItemCount);
        Assert.Equal(0, runs[1].NewItemCount);
        var persistedSource = await dbContext.Sources.SingleAsync();
        Assert.Equal("Succeeded", persistedSource.LastIngestionStatus);
        Assert.Equal("\"job-rss\"", persistedSource.ETag);
    }

    private static string ReadFixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Ingestion", "Fixtures", "job-rss.xml"));

    private sealed class StaticResolver : IFeedHostAddressResolver
    {
        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Parse("8.8.8.8")]);
    }

    private sealed class FixtureHttpClientFactory(HttpMessageHandler handler) : IFeedValidationHttpClientFactory
    {
        public HttpClient Create(Uri feedUrl, IPAddress validatedAddress)
        {
            var client = new HttpClient(handler, disposeHandler: false)
            {
                Timeout = Timeout.InfiniteTimeSpan,
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalTechBrief/1.0");
            return client;
        }
    }

    private sealed class FixtureHandler(string fixture) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers = { ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"job-rss\"") },
                Content = new StringContent(fixture, Encoding.UTF8, "application/rss+xml"),
            });
        }
    }
}
