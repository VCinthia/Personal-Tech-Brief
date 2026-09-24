using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.DependencyInjection;
using PersonalTechBrief.Web.Health;

namespace PersonalTechBrief.IntegrationTests.Configuration;

public class WebUserSecretsStartupTests
{
    [Fact]
    public async Task Development_startup_loads_the_documented_user_secrets_configuration_path()
    {
        using var factory = new DevelopmentWebApplicationFactory();
        using var client = factory.CreateClient();

        var configurationRoot = Assert.IsAssignableFrom<IConfigurationRoot>(
            factory.Services.GetRequiredService<IConfiguration>());

        Assert.Contains(
            configurationRoot.Providers,
            provider => string.Equals(
                provider.ToString(),
                "JsonConfigurationProvider for 'secrets.json' (Optional)",
                StringComparison.Ordinal));

        using var response = await client.GetAsync(HealthEndpointPaths.Live);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public void Web_host_has_a_stable_non_secret_user_secrets_identifier()
    {
        var userSecretsId = typeof(Program).Assembly
            .GetCustomAttributes(typeof(UserSecretsIdAttribute), inherit: false)
            .OfType<UserSecretsIdAttribute>()
            .Single()
            .UserSecretsId;

        Assert.Equal("personal-tech-brief-web-6fcb7b71-78e3-4d10-bc7d-8ac4cd04d313", userSecretsId);
    }

    private sealed class DevelopmentWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }
    }
}
