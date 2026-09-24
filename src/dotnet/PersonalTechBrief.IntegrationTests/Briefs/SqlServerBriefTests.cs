using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTechBrief.Application.Briefs;
using PersonalTechBrief.Domain.Analysis;
using PersonalTechBrief.Domain.Briefs;
using PersonalTechBrief.Domain.Interests;
using PersonalTechBrief.Domain.Ingestion;
using PersonalTechBrief.Domain.Sources;
using PersonalTechBrief.Infrastructure.Persistence;
using PersonalTechBrief.IntegrationTests.Interests;

namespace PersonalTechBrief.IntegrationTests.Briefs;

// Real-SQL coverage for brief generation (FR-011/012/014/018, AC-006/007/008/009/010): the migration
// applies; POST returns 202 and generation completes asynchronously to an immutable snapshot; the
// snapshot is not rewritten by later re-analysis; a candidate already represented in a completed brief
// is excluded from later briefs; and a brief with no eligible candidate completes with SelectedCount 0.
// The Intelligence API is a deterministic fake — no live LLM.
public sealed class SqlServerBriefTests(SqlServerInterestApiFixture fixture) : IClassFixture<SqlServerInterestApiFixture>
{
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Create_returns_202_and_generation_persists_an_immutable_snapshot()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        factory.GenerationClient.Reset();

        var updateId = await SeedCandidateAsync(factory, score: 80d, topic: "runtime-platforms");
        using var client = factory.CreateClient();

        var briefId = await CreateBriefAsync(client);
        var brief = await WaitForCompletionAsync(client, briefId);

        Assert.Equal("completed", brief.GetProperty("brief").GetProperty("status").GetString());
        var items = brief.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        var item = items[0];
        Assert.Equal(updateId, item.GetProperty("technologyUpdateId").GetGuid());
        Assert.Equal(1, item.GetProperty("rank").GetInt32());
        Assert.Equal("runtime-platforms", item.GetProperty("topic").GetString());
        Assert.Equal(80d, item.GetProperty("relevanceScore").GetDouble());
        Assert.StartsWith("Brief:", item.GetProperty("title").GetString());
        var sources = item.GetProperty("sources");
        Assert.True(sources.GetArrayLength() >= 1);
        Assert.False(string.IsNullOrWhiteSpace(sources[0].GetProperty("url").GetString()));

        // The migration is applied.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
            var applied = await db.Database.GetAppliedMigrationsAsync();
            Assert.Contains(applied, migration => migration.EndsWith("AddBriefs", StringComparison.Ordinal));
        }

        var snapshotTitle = item.GetProperty("title").GetString();

        // Re-analysis mutates the live TechnologyUpdate; the historical brief must not change.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
            var update = await db.TechnologyUpdates.SingleAsync(u => u.Id == updateId);
            update.SetRelevanceScore(12d, DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        // GET current returns the same, unchanged snapshot.
        using var currentResponse = await client.GetAsync("/api/v1/briefs/current");
        currentResponse.EnsureSuccessStatusCode();
        var current = await currentResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(briefId, current.GetProperty("brief").GetProperty("id").GetGuid());
        var currentItem = current.GetProperty("items")[0];
        Assert.Equal(80d, currentItem.GetProperty("relevanceScore").GetDouble());
        Assert.Equal(snapshotTitle, currentItem.GetProperty("title").GetString());

        // History lists the brief.
        using var historyResponse = await client.GetAsync("/api/v1/briefs?limit=10");
        historyResponse.EnsureSuccessStatusCode();
        var history = await historyResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            history.GetProperty("briefs").EnumerateArray(),
            summary => summary.GetProperty("id").GetGuid() == briefId);
    }

    [Fact]
    public async Task Already_briefed_updates_are_excluded_and_an_empty_brief_persists_selected_count_zero()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        factory.GenerationClient.Reset();

        var updateA = await SeedCandidateAsync(factory, score: 80d, topic: "topic-a");
        using var client = factory.CreateClient();

        // First brief selects update A.
        var brief1Id = await CreateBriefAsync(client);
        var brief1 = await WaitForCompletionAsync(client, brief1Id);
        Assert.Equal(1, brief1.GetProperty("items").GetArrayLength());
        Assert.Equal(updateA, brief1.GetProperty("items")[0].GetProperty("technologyUpdateId").GetGuid());

        // Bump A into the second brief's window, and add a new qualifying update B.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
            var a = await db.TechnologyUpdates.SingleAsync(u => u.Id == updateA);
            a.RegisterSupportingObservation(DateTime.UtcNow, DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        var updateB = await SeedCandidateAsync(factory, score: 70d, topic: "topic-b");

        // Second brief excludes the already-briefed A and selects only B.
        var brief2Id = await CreateBriefAsync(client);
        var brief2 = await WaitForCompletionAsync(client, brief2Id);
        var brief2Items = brief2.GetProperty("items");
        Assert.Equal(1, brief2Items.GetArrayLength());
        Assert.Equal(updateB, brief2Items[0].GetProperty("technologyUpdateId").GetGuid());
        Assert.Equal(1, brief2.GetProperty("brief").GetProperty("selectedCount").GetInt32());

        // Third brief has no remaining eligible candidate: a valid empty brief with SelectedCount 0.
        var brief3Id = await CreateBriefAsync(client);
        var brief3 = await WaitForCompletionAsync(client, brief3Id);
        Assert.Equal("completed", brief3.GetProperty("brief").GetProperty("status").GetString());
        Assert.Equal(0, brief3.GetProperty("brief").GetProperty("selectedCount").GetInt32());
        Assert.Equal(0, brief3.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task A_generating_brief_does_not_shadow_the_last_completed_current()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();

        Guid completedId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();

            var completed = NewBrief(DateTime.UtcNow.AddMinutes(-1));
            completed.Complete(candidateCount: 0, []);
            completedId = completed.Id;
            db.Briefs.Add(completed);

            // A later-timestamped brief stuck in Generating must never hide the completed one.
            db.Briefs.Add(NewBrief(DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/briefs/current");
        response.EnsureSuccessStatusCode();
        var current = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(completedId, current.GetProperty("brief").GetProperty("id").GetGuid());
        Assert.Equal("completed", current.GetProperty("brief").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Startup_reconciliation_fails_a_stale_generating_brief()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();

        var cutoffUtc = DateTime.UtcNow;
        Guid staleId;
        Guid racingId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
            // A stale orphan created before the cutoff, and a "racing" brief created after it
            // (as if a POST landed while reconciliation ran) which must NOT be swept.
            var stale = NewBrief(cutoffUtc.AddMinutes(-1));
            var racing = NewBrief(cutoffUtc.AddMinutes(1));
            staleId = stale.Id;
            racingId = racing.Id;
            db.Briefs.Add(stale);
            db.Briefs.Add(racing);
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IBriefRepository>();
            var reconciled = await repository.FailOrphanedGeneratingBriefsAsync(cutoffUtc, CancellationToken.None);
            Assert.Equal(1, reconciled);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
            var staleStatus = await db.Briefs.Where(b => b.Id == staleId).Select(b => b.Status).SingleAsync();
            var racingStatus = await db.Briefs.Where(b => b.Id == racingId).Select(b => b.Status).SingleAsync();
            Assert.Equal(BriefStatus.Failed, staleStatus);
            Assert.Equal(BriefStatus.Generating, racingStatus);
        }
    }

    [Fact]
    public async Task Current_and_by_id_return_404_when_absent()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        using var currentResponse = await client.GetAsync("/api/v1/briefs/current");
        Assert.Equal(HttpStatusCode.NotFound, currentResponse.StatusCode);
        Assert.Equal("application/problem+json", currentResponse.Content.Headers.ContentType?.MediaType);

        using var byIdResponse = await client.GetAsync($"/api/v1/briefs/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, byIdResponse.StatusCode);
    }

    private static async Task<Guid> CreateBriefAsync(HttpClient client)
    {
        using var response = await client.PostAsync("/api/v1/briefs", content: null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> WaitForCompletionAsync(HttpClient client, Guid briefId)
    {
        var deadline = DateTime.UtcNow + PollTimeout;
        while (DateTime.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"/api/v1/briefs/{briefId}");
            response.EnsureSuccessStatusCode();
            var brief = await response.Content.ReadFromJsonAsync<JsonElement>();
            var status = brief.GetProperty("brief").GetProperty("status").GetString();
            if (!string.Equals(status, "generating", StringComparison.OrdinalIgnoreCase))
            {
                return brief.Clone();
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Brief {briefId} did not complete within {PollTimeout}.");
    }

    private static Brief NewBrief(DateTime generatedAtUtc) =>
        Brief.Create(generatedAtUtc, generatedAtUtc.AddDays(-7), generatedAtUtc, "brief-test", Guid.NewGuid());

    private static async Task<Guid> SeedCandidateAsync(SqlServerInterestApiFactory factory, double score, string topic)
    {
        // Observe at "now" so the update falls inside the window of a brief generated moments later
        // (a brief's window starts at the previous completed brief's generation time, §20).
        var now = DateTime.UtcNow;
        var observedAtUtc = now;

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();

        var source = Source.Create($"Feed {Guid.NewGuid():N}", $"https://feeds.example.test/{Guid.NewGuid():N}.xml", now.AddDays(-2));
        db.Sources.Add(source);

        var sourceItem = SourceItem.CreateFeed(
            source.Id,
            externalId: Guid.NewGuid().ToString("N"),
            sourceUrl: $"https://news.example.test/{Guid.NewGuid():N}",
            title: "Kubernetes 1.40 released",
            excerpt: "A platform release with a critical runtime security fix.",
            publishedAtUtc: observedAtUtc,
            contentHash: Guid.NewGuid().ToString("N"),
            retrievedAtUtc: observedAtUtc);
        sourceItem.MarkProcessed();
        db.SourceItems.Add(sourceItem);

        var interest = Interest.Create($"Platform engineering {Guid.NewGuid():N}", InterestPriority.High, now.AddDays(-3));
        db.Interests.Add(interest);

        var update = TechnologyUpdate.Create("Kubernetes 1.40 released", topic, observedAtUtc);
        update.SetRelevanceScore(score, now);
        db.TechnologyUpdates.Add(update);

        db.TechnologyUpdateSources.Add(TechnologyUpdateSource.Link(update.Id, sourceItem.Id, similarityScore: null, now));
        db.UpdateInterestMatches.Add(UpdateInterestMatch.Create(update.Id, interest.Id, matchStrength: 1.0d, now));

        await db.SaveChangesAsync();
        return update.Id;
    }
}
