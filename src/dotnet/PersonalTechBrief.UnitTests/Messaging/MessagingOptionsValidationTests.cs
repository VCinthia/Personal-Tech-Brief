using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Messaging;
using PersonalTechBrief.Infrastructure.Messaging;

namespace PersonalTechBrief.UnitTests.Messaging;

// Guards the Processor host startup: OutboxDispatchOptions cross-validates against ServiceBusOptions
// via the Validate<TDep> overload, which resolves ServiceBusOptions from the container. If the bound
// value is not registered as a service, ValidateOnStart throws at host start and the Processor cannot
// run (its Program never gets covered by the Web-focused integration tests).
public sealed class MessagingOptionsValidationTests
{
    [Fact]
    public void Outbox_dispatch_options_validate_against_service_bus_options()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceBus:ConnectionString"] =
                    "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddMessagingInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        // The bound value must be resolvable for the cross-options validation to run.
        Assert.NotNull(provider.GetRequiredService<ServiceBusOptions>());

        // Accessing the value triggers every registered validation, including the cross-options one.
        var dispatch = provider.GetRequiredService<IOptions<OutboxDispatchOptions>>().Value;
        var serviceBus = provider.GetRequiredService<IOptions<ServiceBusOptions>>().Value;
        Assert.True(dispatch.LeaseDurationSeconds > serviceBus.OperationTimeoutSeconds);
    }
}
