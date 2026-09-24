using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PersonalTechBrief.Infrastructure.Intelligence;

public static class IntelligenceApiDependencyInjection
{
    /// <summary>
    /// Registers the typed <see cref="IIntelligenceApiClient"/> backed by
    /// <see cref="System.Net.Http.IHttpClientFactory"/>. Options (base address, per-endpoint
    /// timeouts, retry/backoff) are bound from the <see cref="IntelligenceApiOptions.SectionName"/>
    /// configuration section and validated on start; the base address is never hardcoded.
    /// </summary>
    public static IServiceCollection AddIntelligenceApiClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IntelligenceApiOptions>()
            .Bind(configuration.GetSection(IntelligenceApiOptions.SectionName))
            .Validate(
                options => options.BaseAddress is not null && options.BaseAddress.IsAbsoluteUri,
                "Intelligence API base address must be configured as an absolute URI.")
            .Validate(
                options => options.AnalyzeTimeoutSeconds is > 0 and <= 120,
                "Intelligence API analyze timeout must be between 1 and 120 seconds.")
            .Validate(
                options => options.SimilarityTimeoutSeconds is > 0 and <= 120,
                "Intelligence API similarity timeout must be between 1 and 120 seconds.")
            .Validate(
                options => options.GenerateTimeoutSeconds is > 0 and <= 120,
                "Intelligence API generate timeout must be between 1 and 120 seconds.")
            .Validate(
                options => options.MaxRetries is >= 0 and <= 5,
                "Intelligence API retry count must be between 0 and 5.")
            .Validate(
                options => options.RetryBaseDelayMilliseconds is >= 0 and <= 10_000,
                "Intelligence API retry base delay must be between 0 and 10000 milliseconds.")
            .ValidateOnStart();

        services.AddHttpClient<IIntelligenceApiClient, HttpIntelligenceApiClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<IntelligenceApiOptions>>().Value;
            var baseAddress = options.BaseAddress!;

            // Ensure relative endpoint paths resolve under the configured base address regardless of
            // whether the operator supplied a trailing slash.
            client.BaseAddress = baseAddress.AbsoluteUri.EndsWith('/')
                ? baseAddress
                : new Uri(baseAddress.AbsoluteUri + "/", UriKind.Absolute);

            // Per-request timeouts are enforced inside the client so analyze and similarity can differ.
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        return services;
    }
}
