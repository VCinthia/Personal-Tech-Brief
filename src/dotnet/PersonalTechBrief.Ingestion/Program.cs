using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonalTechBrief.Application;
using PersonalTechBrief.Ingestion;
using PersonalTechBrief.Ingestion.Feeds;
using PersonalTechBrief.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOptions<FeedRetrievalOptions>()
    .Bind(builder.Configuration.GetSection(FeedRetrievalOptions.SectionName))
    .Validate(
        options => options.TimeoutSeconds is > 0 and <= 60,
        "Feed retrieval timeout must be between 1 and 60 seconds.")
    .Validate(
        options => options.MaximumResponseBytes is > 0 and <= 10_485_760,
        "Feed retrieval response limit must be between 1 byte and 10 MB.")
    .Validate(
        options => options.MaximumAttempts is > 0 and <= 5,
        "Feed retrieval attempts must be between 1 and 5.")
    .Validate(
        options => options.RetryDelayMilliseconds is >= 0 and <= 30_000,
        "Feed retrieval retry delay must be between 0 and 30 seconds.")
    .ValidateOnStart();
builder.Services.AddSingleton<IFeedRetrievalClient, RssAtomFeedRetrievalClient>();
builder.Services.AddScoped<IngestionJob>();

using var host = builder.Build();

await host.StartAsync();
var exitCode = 0;

using (var scope = host.Services.CreateScope())
{
    var job = scope.ServiceProvider.GetRequiredService<IngestionJob>();
    var result = await job.RunOnceAsync(
        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
    exitCode = result.ExitCode;
    scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("PersonalTechBrief.Ingestion")
        .LogInformation(
            "Finite ingestion job finished. EnabledSourceCount {EnabledSourceCount}, SucceededSourceCount {SucceededSourceCount}, FailedSourceCount {FailedSourceCount}, NewItemCount {NewItemCount}",
            result.EnabledSourceCount,
            result.SucceededSourceCount,
            result.FailedSourceCount,
            result.NewItemCount);
}

await host.StopAsync();
return exitCode;
