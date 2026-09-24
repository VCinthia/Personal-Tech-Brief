using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTechBrief.Domain.Analysis;
using PersonalTechBrief.Domain.Briefs;
using PersonalTechBrief.Domain.Ingestion;
using PersonalTechBrief.Domain.Interactions;
using PersonalTechBrief.Domain.Sources;
using PersonalTechBrief.Infrastructure.Persistence;
using PersonalTechBrief.IntegrationTests.Interests;

namespace PersonalTechBrief.IntegrationTests.Interactions;

// Real-SQL coverage for the FR-013 interaction subset and the FR-016 evaluation summary
// (UC-009/010/011, AC-012/013/014): the migration applies; feedback is persisted, attributable and the
// latest supersedes; save/unsave is idempotent; source-open is rejected for a non-supporting source;
// the evaluation summary counts seeded data; the documented status codes are returned; and interactions
// never mutate an existing Brief/BriefItem snapshot.
public sealed class SqlServerInteractionApiTests(SqlServerInterestApiFixture fixture)
    : IClassFixture<SqlServerInterestApiFixture>
{
    [Fact]
    public async Task Migration_is_applied()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, migration => migration.EndsWith("AddInteractionsAndEvaluation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Feedback_is_persisted_attributable_and_the_latest_supersedes()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        var seed = await SeedCompletedBriefAsync(factory, supportingSourceCount: 1);
        using var client = factory.CreateClient();

        using (var first = await client.PutAsJsonAsync(
            $"/api/v1/updates/{seed.UpdateId}/feedback",
            new { value = "relevant", briefItemId = seed.BriefItemId }))
        {
            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        }

        await Task.Delay(15); // Guarantee a strictly later timestamp so "latest" is unambiguous.

        using (var second = await client.PutAsJsonAsync(
            $"/api/v1/updates/{seed.UpdateId}/feedback",
            new { value = "notRelevant", briefItemId = seed.BriefItemId }))
        {
            Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        var rows = await db.UserFeedback.AsNoTracking()
            .Where(feedback => feedback.TechnologyUpdateId == seed.UpdateId)
            .OrderBy(feedback => feedback.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(seed.BriefItemId, row.BriefItemId));
        Assert.Equal(FeedbackType.Relevant, rows[0].FeedbackType);
        Assert.Equal(FeedbackType.NotRelevant, rows[^1].FeedbackType);

        // The evaluation tally uses the latest row per update.
        var summary = await GetSummaryAsync(client);
        Assert.Equal(0, summary.GetProperty("relevantFeedback").GetInt32());
        Assert.Equal(1, summary.GetProperty("notRelevantFeedback").GetInt32());
    }

    [Fact]
    public async Task Brief_read_surfaces_the_current_feedback_and_saved_state()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        var seed = await SeedCompletedBriefAsync(factory, supportingSourceCount: 1);
        using var client = factory.CreateClient();

        using (var feedback = await client.PutAsJsonAsync(
            $"/api/v1/updates/{seed.UpdateId}/feedback",
            new { value = "relevant", briefItemId = seed.BriefItemId }))
        {
            Assert.Equal(HttpStatusCode.NoContent, feedback.StatusCode);
        }

        using (var saved = await client.PutAsync($"/api/v1/updates/{seed.UpdateId}/saved", content: null))
        {
            Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        }

        // The brief read model hydrates the live interaction state without mutating the snapshot.
        using var response = await client.GetAsync($"/api/v1/briefs/{seed.BriefId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = body.GetProperty("items").EnumerateArray()
            .Single(entry => entry.GetProperty("technologyUpdateId").GetGuid() == seed.UpdateId);
        Assert.Equal("relevant", item.GetProperty("currentFeedback").GetString());
        Assert.True(item.GetProperty("isSaved").GetBoolean());
    }

    [Fact]
    public async Task Brief_read_reports_no_feedback_and_unsaved_before_any_interaction()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        var seed = await SeedCompletedBriefAsync(factory, supportingSourceCount: 1);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/v1/briefs/{seed.BriefId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = body.GetProperty("items").EnumerateArray()
            .Single(entry => entry.GetProperty("technologyUpdateId").GetGuid() == seed.UpdateId);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("currentFeedback").ValueKind);
        Assert.False(item.GetProperty("isSaved").GetBoolean());
    }

    [Fact]
    public async Task Clearing_feedback_removes_the_current_feedback()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        var seed = await SeedCompletedBriefAsync(factory, supportingSourceCount: 1);
        using var client = factory.CreateClient();

        using (var put = await client.PutAsJsonAsync(
            $"/api/v1/updates/{seed.UpdateId}/feedback", new { value = "relevant" }))
        {
            Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        }

        using (var delete = await client.DeleteAsync($"/api/v1/updates/{seed.UpdateId}/feedback"))
        {
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        var remaining = await db.UserFeedback.AsNoTracking()
            .CountAsync(feedback => feedback.TechnologyUpdateId == seed.UpdateId);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task Feedback_returns_400_for_a_bad_value_and_404_for_an_unknown_update()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        var seed = await SeedCompletedBriefAsync(factory, supportingSourceCount: 1);
        using var client = factory.CreateClient();

        using (var badValue = await client.PutAsJsonAsync(
            $"/api/v1/updates/{seed.UpdateId}/feedback", new { value = "maybe" }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, badValue.StatusCode);
            Assert.Equal("application/problem+json", badValue.Content.Headers.ContentType?.MediaType);
        }

        using var unknown = await client.PutAsJsonAsync(
            $"/api/v1/updates/{Guid.NewGuid()}/feedback", new { value = "relevant" });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("application/problem+json", unknown.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Save_and_unsave_are_idempotent_and_return_the_documented_status_codes()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        var seed = await SeedCompletedBriefAsync(factory, supportingSourceCount: 1);
        using var client = factory.CreateClient();

        using (var save1 = await client.PutAsync($"/api/v1/updates/{seed.UpdateId}/saved", content: null))
        {
            Assert.Equal(HttpStatusCode.NoContent, save1.StatusCode);
        }

        using (var save2 = await client.PutAsync($"/api/v1/updates/{seed.UpdateId}/saved", content: null))
        {
            Assert.Equal(HttpStatusCode.NoContent, save2.StatusCode);
        }

        Assert.Equal(1, await CountSavedAsync(factory, seed.UpdateId));

        using (var unsave1 = await client.DeleteAsync($"/api/v1/updates/{seed.UpdateId}/saved"))
        {
            Assert.Equal(HttpStatusCode.NoContent, unsave1.StatusCode);
        }

        // Deleting an absent save is still 204 (idempotent).
        using (var unsave2 = await client.DeleteAsync($"/api/v1/updates/{seed.UpdateId}/saved"))
        {
            Assert.Equal(HttpStatusCode.NoContent, unsave2.StatusCode);
        }

        Assert.Equal(0, await CountSavedAsync(factory, seed.UpdateId));

        using var unknown = await client.PutAsync($"/api/v1/updates/{Guid.NewGuid()}/saved", content: null);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Source_open_is_rejected_for_a_non_supporting_source_and_recorded_for_a_supporting_one()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        var seed = await SeedCompletedBriefAsync(factory, supportingSourceCount: 1);
        using var client = factory.CreateClient();

        // A source item that does not support the update is rejected.
        using (var notSupporting = await client.PostAsync(
            $"/api/v1/updates/{seed.UpdateId}/sources/{Guid.NewGuid()}/open", content: null))
        {
            Assert.Equal(HttpStatusCode.NotFound, notSupporting.StatusCode);
            Assert.Equal("application/problem+json", notSupporting.Content.Headers.ContentType?.MediaType);
        }

        // A supporting source records the telemetry event.
        using (var supporting = await client.PostAsync(
            $"/api/v1/updates/{seed.UpdateId}/sources/{seed.PrimarySourceItemId}/open", content: null))
        {
            Assert.Equal(HttpStatusCode.Accepted, supporting.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        var events = await db.SourceOpenEvents.AsNoTracking()
            .Where(sourceOpen => sourceOpen.TechnologyUpdateId == seed.UpdateId)
            .ToListAsync();
        var recorded = Assert.Single(events);
        Assert.Equal(seed.PrimarySourceItemId, recorded.SourceItemId);
    }

    [Fact]
    public async Task Evaluation_summary_counts_seeded_data()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        // Two supporting sources on one update => one grouped duplicate; two ingested items.
        var seed = await SeedCompletedBriefAsync(factory, supportingSourceCount: 2);
        using var client = factory.CreateClient();

        using (var save = await client.PutAsync($"/api/v1/updates/{seed.UpdateId}/saved", content: null))
        {
            Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);
        }

        using (var open = await client.PostAsync(
            $"/api/v1/updates/{seed.UpdateId}/sources/{seed.PrimarySourceItemId}/open", content: null))
        {
            Assert.Equal(HttpStatusCode.Accepted, open.StatusCode);
        }

        using (var feedback = await client.PutAsJsonAsync(
            $"/api/v1/updates/{seed.UpdateId}/feedback", new { value = "relevant" }))
        {
            Assert.Equal(HttpStatusCode.NoContent, feedback.StatusCode);
        }

        var summary = await GetSummaryAsync(client);
        Assert.Equal(2, summary.GetProperty("itemsIngested").GetInt32());
        Assert.Equal(1, summary.GetProperty("itemsSelected").GetInt32());
        Assert.Equal(1, summary.GetProperty("groupedDuplicates").GetInt32());
        Assert.Equal(1, summary.GetProperty("relevantFeedback").GetInt32());
        Assert.Equal(0, summary.GetProperty("notRelevantFeedback").GetInt32());
        Assert.Equal(1, summary.GetProperty("sourceOpens").GetInt32());
        Assert.Equal(1, summary.GetProperty("saves").GetInt32());
        Assert.Equal(1, summary.GetProperty("briefCount").GetInt32());
        Assert.Equal(1d, summary.GetProperty("itemsPerBrief").GetDouble());

        var distribution = summary.GetProperty("sourceDistribution");
        var row = Assert.Single(distribution.EnumerateArray());
        Assert.Equal(seed.SourceId, row.GetProperty("sourceId").GetGuid());
        Assert.Equal(2, row.GetProperty("ingestedItems").GetInt32());
        // Only the source item captured as a brief item source counts as "selected".
        Assert.Equal(1, row.GetProperty("selectedItems").GetInt32());
    }

    [Fact]
    public async Task Interactions_never_mutate_an_existing_brief_snapshot()
    {
        fixture.ThrowIfDockerIsUnavailable();
        var factory = Assert.IsType<SqlServerInterestApiFactory>(fixture.Factory);
        await factory.ResetDatabaseAsync();
        var seed = await SeedCompletedBriefAsync(factory, supportingSourceCount: 1);
        using var client = factory.CreateClient();

        var before = await (await client.GetAsync($"/api/v1/briefs/{seed.BriefId}")).Content.ReadAsStringAsync();

        using (var feedback = await client.PutAsJsonAsync(
            $"/api/v1/updates/{seed.UpdateId}/feedback",
            new { value = "notRelevant", briefItemId = seed.BriefItemId }))
        {
            Assert.Equal(HttpStatusCode.NoContent, feedback.StatusCode);
        }

        using (var save = await client.PutAsync($"/api/v1/updates/{seed.UpdateId}/saved", content: null))
        {
            Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);
        }

        using (var open = await client.PostAsync(
            $"/api/v1/updates/{seed.UpdateId}/sources/{seed.PrimarySourceItemId}/open", content: null))
        {
            Assert.Equal(HttpStatusCode.Accepted, open.StatusCode);
        }

        var after = await (await client.GetAsync($"/api/v1/briefs/{seed.BriefId}")).Content.ReadAsStringAsync();

        // The live interaction state is surfaced (and now reflects the actions)...
        var afterNode = JsonNode.Parse(after)!;
        var afterItem = afterNode["items"]!.AsArray()
            .Single(entry => entry!["technologyUpdateId"]!.GetValue<Guid>() == seed.UpdateId)!;
        Assert.Equal("notRelevant", (string?)afterItem["currentFeedback"]);
        Assert.True((bool)afterItem["isSaved"]!);

        // ...but the immutable snapshot fields are byte-for-byte unchanged once the live state is removed.
        var beforeNode = JsonNode.Parse(before)!;
        StripLiveInteractionState(beforeNode);
        StripLiveInteractionState(afterNode);
        Assert.True(
            JsonNode.DeepEquals(beforeNode, afterNode),
            "Interactions must not rewrite the immutable brief snapshot.");
    }

    private static void StripLiveInteractionState(JsonNode brief)
    {
        foreach (var item in brief["items"]!.AsArray())
        {
            item!.AsObject().Remove("currentFeedback");
            item.AsObject().Remove("isSaved");
        }
    }

    private static async Task<JsonElement> GetSummaryAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/evaluation/summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.Clone();
    }

    private static async Task<int> CountSavedAsync(SqlServerInterestApiFactory factory, Guid updateId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();
        return await db.SavedUpdates.AsNoTracking().CountAsync(saved => saved.TechnologyUpdateId == updateId);
    }

    private static async Task<Seed> SeedCompletedBriefAsync(SqlServerInterestApiFactory factory, int supportingSourceCount)
    {
        var now = DateTime.UtcNow;

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PersonalTechBriefDbContext>();

        var source = Source.Create($"Feed {Guid.NewGuid():N}", $"https://feeds.example.test/{Guid.NewGuid():N}.xml", now.AddDays(-2));
        db.Sources.Add(source);

        var update = TechnologyUpdate.Create("Kubernetes 1.40 released", "runtime-platforms", now);
        update.SetRelevanceScore(80d, now);
        db.TechnologyUpdates.Add(update);

        var sourceItemIds = new List<Guid>();
        for (var index = 0; index < supportingSourceCount; index++)
        {
            var sourceItem = SourceItem.CreateFeed(
                source.Id,
                externalId: Guid.NewGuid().ToString("N"),
                sourceUrl: $"https://news.example.test/{Guid.NewGuid():N}",
                title: $"Supporting article {index}",
                excerpt: "A platform release with a critical runtime security fix.",
                publishedAtUtc: now,
                contentHash: Guid.NewGuid().ToString("N"),
                retrievedAtUtc: now);
            sourceItem.MarkProcessed();
            db.SourceItems.Add(sourceItem);
            sourceItemIds.Add(sourceItem.Id);
            db.TechnologyUpdateSources.Add(TechnologyUpdateSource.Link(update.Id, sourceItem.Id, similarityScore: null, now));
        }

        var primarySourceItemId = sourceItemIds[0];

        var brief = Brief.Create(now, now.AddDays(-7), now, "brief-test", Guid.NewGuid());
        var item = BriefItem.Create(
            brief.Id,
            update.Id,
            rank: 1,
            titleSnapshot: "Kubernetes 1.40 released",
            topicSnapshot: "runtime-platforms",
            summarySnapshot: "A concise factual summary captured at generation time.",
            whyRelevantSnapshot: "Relevant to your platform-engineering interests.",
            relevanceScoreSnapshot: 80d,
            generatedAtUtc: now,
            promptVersion: "p1",
            modelOrAlgorithmVersion: "m1",
            sources: [new BriefItemSourceSnapshot(primarySourceItemId, "Supporting article 0", "https://news.example.test/original", now)]);
        brief.Complete(candidateCount: supportingSourceCount, [item]);
        db.Briefs.Add(brief);

        await db.SaveChangesAsync();
        return new Seed(update.Id, item.Id, primarySourceItemId, source.Id, brief.Id);
    }

    private sealed record Seed(Guid UpdateId, Guid BriefItemId, Guid PrimarySourceItemId, Guid SourceId, Guid BriefId);
}
