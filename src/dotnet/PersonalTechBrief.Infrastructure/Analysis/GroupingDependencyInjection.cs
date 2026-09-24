using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalTechBrief.Application.Analysis;

namespace PersonalTechBrief.Infrastructure.Analysis;

public static class GroupingDependencyInjection
{
    /// <summary>
    /// Registers the deterministic relevance scorer, the grouping persistence boundary, and the
    /// grouping/relevance pipeline service, binding and validating their configuration. The typed
    /// Intelligence API client is registered separately via <c>AddIntelligenceApiClient</c>.
    /// </summary>
    public static IServiceCollection AddGroupingAndRelevance(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RelevanceScoringOptions>()
            .Bind(configuration.GetSection(RelevanceScoringOptions.SectionName))
            .Validate(
                options => options.SelectionThreshold >= 0d,
                "The relevance selection threshold must be non-negative.")
            .Validate(
                options => options.AdditionalInterestBonusCap >= 0d,
                "The additional-interest bonus cap must be non-negative.")
            .Validate(
                options => options.InterestHighWeight >= options.InterestMediumWeight &&
                    options.InterestMediumWeight >= options.InterestLowWeight &&
                    options.InterestLowWeight >= 0d,
                "Interest weights must be ordered High >= Medium >= Low >= 0.")
            .ValidateOnStart();

        services.AddOptions<GroupingPipelineOptions>()
            .Bind(configuration.GetSection(GroupingPipelineOptions.SectionName))
            .Validate(
                options => options.BatchSize is > 0 and <= 100,
                "The grouping pipeline batch size must be between 1 and 100.")
            .Validate(
                options => options.PollIntervalSeconds is > 0 and <= 60,
                "The grouping pipeline poll interval must be between 1 and 60 seconds.")
            .Validate(
                options => options.GroupingWindowHours is > 0 and <= 8760,
                "The grouping window must be between 1 hour and 365 days.")
            .Validate(
                options => options.MaxComparisons is > 0 and <= 100,
                "The grouping comparison bound must be between 1 and 100.")
            .Validate(
                options => options.MergeThreshold is >= 0d and <= 1d,
                "The merge threshold must be within [0, 1].")
            .Validate(
                options => options.MaxProcessingAttempts is > 0 and <= 20,
                "The bounded-retry budget must be between 1 and 20 attempts.")
            .ValidateOnStart();

        services.AddSingleton<IRelevanceScorer, RelevanceScorer>();
        services.AddScoped<IGroupingRepository, SqlGroupingRepository>();
        services.AddScoped<IGroupingPipelineService, GroupingPipelineService>();
        return services;
    }
}
