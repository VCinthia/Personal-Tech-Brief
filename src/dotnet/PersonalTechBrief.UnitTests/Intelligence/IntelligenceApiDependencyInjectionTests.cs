using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Infrastructure.Intelligence;

namespace PersonalTechBrief.UnitTests.Intelligence;

public class IntelligenceApiDependencyInjectionTests
{
    [Fact]
    public void AddIntelligenceApiClient_registers_typed_client_and_binds_options()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IntelligenceApi:BaseAddress"] = "https://intelligence.internal.test/",
                ["IntelligenceApi:AnalyzeTimeoutSeconds"] = "30",
                ["IntelligenceApi:SimilarityTimeoutSeconds"] = "10",
                ["IntelligenceApi:MaxRetries"] = "2",
            })
            .Build();

        using var provider = new ServiceCollection()
            .AddLogging()
            .AddIntelligenceApiClient(configuration)
            .BuildServiceProvider();

        var client = provider.GetRequiredService<IIntelligenceApiClient>();
        Assert.IsType<HttpIntelligenceApiClient>(client);

        var options = provider.GetRequiredService<IOptions<IntelligenceApiOptions>>().Value;
        Assert.Equal(new Uri("https://intelligence.internal.test/"), options.BaseAddress);
        Assert.Equal(30, options.AnalyzeTimeoutSeconds);
        Assert.Equal(10, options.SimilarityTimeoutSeconds);
        Assert.Equal(2, options.MaxRetries);
    }

    [Fact]
    public void AddIntelligenceApiClient_rejects_missing_base_address()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IntelligenceApi:AnalyzeTimeoutSeconds"] = "30",
            })
            .Build();

        using var provider = new ServiceCollection()
            .AddLogging()
            .AddIntelligenceApiClient(configuration)
            .BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<IntelligenceApiOptions>>().Value);
        Assert.Contains("base address", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
