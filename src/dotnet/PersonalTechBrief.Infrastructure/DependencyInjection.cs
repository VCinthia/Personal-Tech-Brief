using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalTechBrief.Application.Articles;
using PersonalTechBrief.Application.Evaluation;
using PersonalTechBrief.Application.Ingestion;
using PersonalTechBrief.Application.Interactions;
using PersonalTechBrief.Application.Interests;
using PersonalTechBrief.Application.Sources;
using PersonalTechBrief.Infrastructure.Articles;
using PersonalTechBrief.Infrastructure.Evaluation;
using PersonalTechBrief.Infrastructure.Feeds;
using PersonalTechBrief.Infrastructure.Interactions;
using PersonalTechBrief.Infrastructure.Persistence;

namespace PersonalTechBrief.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PersonalTechBriefDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("PersonalTechBrief");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:PersonalTechBrief must be configured before using persistence.");
            }

            options.UseSqlServer(connectionString, sqlServer => sqlServer.MigrationsAssembly(typeof(PersonalTechBriefDbContext).Assembly.FullName));
        });

        services.AddScoped<IInterestRepository, InterestRepository>();
        services.AddScoped<ISourceRepository, SourceRepository>();
        services.AddScoped<IInteractionRepository, SqlInteractionRepository>();
        services.AddScoped<IEvaluationRepository, SqlEvaluationRepository>();
        services.AddScoped<IIngestionRunService, SqlIngestionRunService>();
        services.AddScoped<ISourceItemPersistenceService, SqlSourceItemPersistenceService>();
        services.AddScoped<IFeedValidator, RssAtomFeedValidator>();
        services.AddSingleton<IFeedHostAddressResolver, DnsFeedHostAddressResolver>();
        services.AddSingleton<IFeedValidationHttpClientFactory, PinnedAddressFeedValidationHttpClientFactory>();
        services.AddOptions<FeedValidationOptions>()
            .Bind(configuration.GetSection(FeedValidationOptions.SectionName))
            .Validate(
                options => options.TimeoutSeconds is > 0 and <= 60,
                "Feed validation timeout must be between 1 and 60 seconds.")
            .Validate(
                options => options.MaximumResponseBytes is > 0 and <= 10_485_760,
                "Feed validation response limit must be between 1 byte and 10 MB.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.UserAgent) && options.UserAgent.Length <= 256,
                "Feed validation user agent must be present and no longer than 256 characters.")
            .ValidateOnStart();
        services.AddOptions<IngestionDeduplicationOptions>()
            .Bind(configuration.GetSection(IngestionDeduplicationOptions.SectionName))
            .Validate(
                options => options.TitleWindowDays is > 0 and <= 31,
                "The ingestion title duplicate window must be between 1 and 31 days.")
            .ValidateOnStart();

        services.AddScoped<IManualArticleSubmissionService, ManualArticleSubmissionService>();
        services.AddScoped<IManualArticleStore, SqlManualArticleStore>();
        services.AddSingleton<IArticleContentFetcher, ArticleContentFetcher>();
        services.AddSingleton<IPinnedArticleRequester, PinnedArticleRequester>();
        services.AddSingleton<IArticleTextExtractor, ArticleTextExtractor>();
        services.AddOptions<ManualArticleFetchOptions>()
            .Bind(configuration.GetSection(ManualArticleFetchOptions.SectionName))
            .Validate(
                options => options.TimeoutSeconds is > 0 and <= 60,
                "Manual article fetch timeout must be between 1 and 60 seconds.")
            .Validate(
                options => options.MaximumResponseBytes is > 0 and <= 10_485_760,
                "Manual article fetch response limit must be between 1 byte and 10 MB.")
            .Validate(
                options => options.MaxRedirects is >= 0 and <= 10,
                "Manual article fetch redirects must be between 0 and 10.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.UserAgent) && options.UserAgent.Length <= 256,
                "Manual article fetch user agent must be present and no longer than 256 characters.")
            .ValidateOnStart();
        return services;
    }
}
