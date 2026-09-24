using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PersonalTechBrief.IntegrationTests.Interests;
using PersonalTechBrief.Web.Health;

namespace PersonalTechBrief.IntegrationTests.Health;

public class WebHealthEndpointTests(InterestApiFactory factory) : IClassFixture<InterestApiFactory>
{
    [Theory]
    [InlineData(HealthEndpointPaths.Live)]
    [InlineData(HealthEndpointPaths.Ready)]
    public async Task Probe_endpoint_returns_success_when_the_database_is_available(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public class WebUnavailableDatabaseHealthEndpointTests
{
    private const string InvalidConnectionString = "Server=127.0.0.1;InvalidSqlServerKeyword=true";

    [Fact]
    public async Task Liveness_remains_process_only_when_the_database_is_misconfigured()
    {
        using var factory = new UnavailableDatabaseWebApplicationFactory();
        using var client = factory.CreateClient();

        using var liveResponse = await client.GetAsync(HealthEndpointPaths.Live);
        using var readyResponse = await client.GetAsync(HealthEndpointPaths.Ready);

        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readyResponse.StatusCode);
    }

    private sealed class UnavailableDatabaseWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PersonalTechBrief"] = InvalidConnectionString,
                }));
        }
    }
}
