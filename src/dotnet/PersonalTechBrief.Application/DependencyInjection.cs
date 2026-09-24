using Microsoft.Extensions.DependencyInjection;
using PersonalTechBrief.Application.Evaluation;
using PersonalTechBrief.Application.Interactions;
using PersonalTechBrief.Application.Interests;
using PersonalTechBrief.Application.Sources;

namespace PersonalTechBrief.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IInterestService, InterestService>();
        services.AddScoped<ISourceService, SourceService>();
        services.AddScoped<IInteractionService, InteractionService>();
        services.AddScoped<IEvaluationService, EvaluationService>();
        return services;
    }
}
