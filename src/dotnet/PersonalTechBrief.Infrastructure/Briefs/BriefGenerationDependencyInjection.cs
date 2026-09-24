using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalTechBrief.Application.Analysis;
using PersonalTechBrief.Application.Briefs;

namespace PersonalTechBrief.Infrastructure.Briefs;

public static class BriefGenerationDependencyInjection
{
    /// <summary>
    /// Registers deterministic candidate selection, the brief persistence boundary, and the brief
    /// generation orchestration, binding and validating their configuration. The selection threshold is
    /// reused from the Slice 5 relevance configuration (<see cref="RelevanceScoringOptions"/>). The typed
    /// Intelligence API client is registered separately via <c>AddIntelligenceApiClient</c>.
    /// </summary>
    public static IServiceCollection AddBriefGeneration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RelevanceScoringOptions>()
            .Bind(configuration.GetSection(RelevanceScoringOptions.SectionName))
            .Validate(
                options => options.SelectionThreshold >= 0d,
                "The relevance selection threshold must be non-negative.")
            .ValidateOnStart();

        services.AddOptions<BriefGenerationOptions>()
            .Bind(configuration.GetSection(BriefGenerationOptions.SectionName))
            .Validate(
                options => options.WindowLookbackDays is > 0 and <= 365,
                "The brief window look-back must be between 1 and 365 days.")
            .Validate(
                options => options.MaxItems is > 0 and <= 50,
                "The brief maximum item count must be between 1 and 50.")
            .Validate(
                options => options.MaxGenerationAttempts is > 0 and <= 10,
                "The brief generation attempt budget must be between 1 and 10.")
            .Validate(
                options => options.RetryBaseDelayMilliseconds is >= 0 and <= 10_000,
                "The brief generation retry base delay must be between 0 and 10000 milliseconds.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.GenerationVersion) && options.GenerationVersion.Length <= 64,
                "The brief generation version must be present and no longer than 64 characters.")
            .ValidateOnStart();

        services.AddSingleton<IBriefCandidateSelector, BriefCandidateSelector>();
        services.AddScoped<IBriefRepository, SqlBriefRepository>();
        services.AddScoped<IBriefGenerationService, BriefGenerationService>();
        return services;
    }
}
