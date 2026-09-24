using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;

namespace PersonalTechBrief.Infrastructure.Messaging;

public sealed class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    /// <summary>
    /// Local/emulator development value. It is loaded from ignored user secrets or
    /// environment configuration and is intentionally absent from repository files.
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Azure production namespace for managed-identity authentication.
    /// </summary>
    public string? FullyQualifiedNamespace { get; init; }

    public int OperationTimeoutSeconds { get; init; } = 15;

    public int ReceiveWaitSeconds { get; init; } = 5;

    public int PrefetchCount { get; init; }
}

internal static class ServiceBusClientFactory
{
    public static ServiceBusClient Create(ServiceBusOptions options)
    {
        var clientOptions = new ServiceBusClientOptions
        {
            RetryOptions = new ServiceBusRetryOptions
            {
                MaxRetries = 0,
                TryTimeout = TimeSpan.FromSeconds(options.OperationTimeoutSeconds),
            },
        };

        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return new ServiceBusClient(options.ConnectionString.Trim(), clientOptions);
        }

        return new ServiceBusClient(
            options.FullyQualifiedNamespace!.Trim(),
            new DefaultAzureCredential(),
            clientOptions);
    }
}
