using System.Security.Cryptography;
using Azure.Messaging.ServiceBus;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using PersonalTechBrief.Application.Messaging;
using Testcontainers.MsSql;
using Testcontainers.ServiceBus;

namespace PersonalTechBrief.IntegrationTests.Messaging;

public sealed class ServiceBusEmulatorFixture : IAsyncLifetime
{
    // Use the same immutable Microsoft images as the repository's local compose stack.
    private const string SqlServerImage =
        "mcr.microsoft.com/mssql/server@sha256:97b448857967be55e005424a660056fe6d51814435804dc07e8f79f028bab5fb";
    private const string ServiceBusImage =
        "mcr.microsoft.com/azure-messaging/servicebus-emulator@sha256:5a96d893b245031740f7d46e0fe5ff282d24b78c4b7d761dd57590f3f010a9b3";
    private const string SqlNetworkAlias = "broker-tests-sql";

    private readonly INetwork network = new NetworkBuilder().Build();
    private MsSqlContainer? sqlServer;
    private ServiceBusContainer? serviceBus;

    public async Task InitializeAsync()
    {
        // This credential exists only in memory and in disposable container environments.
        var password = $"Tih!{Convert.ToHexString(RandomNumberGenerator.GetBytes(24))}a1";
        try
        {
            sqlServer = new MsSqlBuilder(SqlServerImage)
                .WithPassword(password)
                .WithNetwork(network)
                .WithNetworkAliases(SqlNetworkAlias)
                .WithCleanUp(true)
                .Build();
            serviceBus = new ServiceBusBuilder(ServiceBusImage)
                .WithAcceptLicenseAgreement(true)
                .WithMsSqlContainer(network, sqlServer, SqlNetworkAlias, password)
                .WithConfig(Path.Combine(AppContext.BaseDirectory, "Messaging", "Fixtures", "servicebus-config.json"))
                .WithCleanUp(true)
                .Build();

            using var startupTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            await network.CreateAsync(startupTimeout.Token);
            await serviceBus.StartAsync(startupTimeout.Token);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public ServiceBusClient CreateClient() => new(
        serviceBus!.GetConnectionString(),
        new ServiceBusClientOptions
        {
            RetryOptions = new ServiceBusRetryOptions
            {
                MaxRetries = 0,
                TryTimeout = TimeSpan.FromSeconds(15),
            },
        });

    public async Task PurgeQueuesAsync()
    {
        // xUnit serializes methods of the same test class. Drain before each test so a
        // previous failing assertion cannot cause another test to consume its message.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client = CreateClient();
        foreach (var subQueue in new[] { SubQueue.None, SubQueue.DeadLetter })
        {
            await using var receiver = client.CreateReceiver(
                SourceItemReadyEnvelope.Destination,
                new ServiceBusReceiverOptions
                {
                    ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
                    SubQueue = subQueue,
                });
            while (await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(1), timeout.Token) is not null)
            {
            }
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (serviceBus is not null)
            {
                await serviceBus.DisposeAsync();
            }
        }
        finally
        {
            try
            {
                if (sqlServer is not null)
                {
                    await sqlServer.DisposeAsync();
                }
            }
            finally
            {
                await network.DisposeAsync();
            }
        }
    }
}
