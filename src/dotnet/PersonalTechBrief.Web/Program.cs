using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using System.Text.Json.Serialization;
using PersonalTechBrief.Application;
using PersonalTechBrief.Infrastructure;
using PersonalTechBrief.Infrastructure.Briefs;
using PersonalTechBrief.Infrastructure.Intelligence;
using PersonalTechBrief.Web.Articles;
using PersonalTechBrief.Web.Briefs;
using PersonalTechBrief.Web.Components;
using PersonalTechBrief.Web.Evaluation;
using PersonalTechBrief.Web.Health;
using PersonalTechBrief.Web.Interactions;
using PersonalTechBrief.Web.Interests;
using PersonalTechBrief.Web.Sources;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIntelligenceApiClient(builder.Configuration);
builder.Services.AddBriefGeneration(builder.Configuration);
builder.Services.AddSingleton<IBriefGenerationQueue, BriefGenerationQueue>();
builder.Services.AddHostedService<BriefGenerationBackgroundService>();
builder.Services.AddHealthChecks()
    .AddCheck<SqlServerReadinessHealthCheck>(
        name: "sql-server",
        failureStatus: HealthStatus.Unhealthy,
        tags: [HealthCheckTags.Ready]);
builder.Services.AddHttpClient();
builder.Services.AddOpenApi();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(async statusCodeContext =>
{
    var response = statusCodeContext.HttpContext.Response;
    await Results.Problem(
            statusCode: response.StatusCode,
            title: "The request could not be processed.")
        .ExecuteAsync(statusCodeContext.HttpContext);
});

app.MapHealthChecks(HealthEndpointPaths.Live, new HealthCheckOptions
{
    Predicate = static _ => false,
});
app.MapHealthChecks(HealthEndpointPaths.Ready, new HealthCheckOptions
{
    Predicate = static check => check.Tags.Contains(HealthCheckTags.Ready),
});
app.UseAntiforgery();
app.MapOpenApi();
app.MapInterestEndpoints();
app.MapSourceEndpoints();
app.MapArticleEndpoints();
app.MapBriefEndpoints();
app.MapInteractionEndpoints();
app.MapEvaluationEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
