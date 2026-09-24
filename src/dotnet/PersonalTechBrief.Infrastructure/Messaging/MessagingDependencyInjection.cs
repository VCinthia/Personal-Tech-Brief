using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PersonalTechBrief.Application.Messaging;

namespace PersonalTechBrief.Infrastructure.Messaging;

public static class MessagingDependencyInjection
{
    public static IServiceCollection AddMessagingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ServiceBusOptions>()
            .Bind(configuration.GetSection(ServiceBusOptions.SectionName))
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.ConnectionString) ||
                    !string.IsNullOrWhiteSpace(options.FullyQualifiedNamespace),
                "Service Bus requires either a connection string or fully qualified namespace.")
            .Validate(
                options =>
                    string.IsNullOrWhiteSpace(options.ConnectionString) ||
                    string.IsNullOrWhiteSpace(options.FullyQualifiedNamespace),
                "Service Bus configuration must choose either connection string or fully qualified namespace, not both.")
            .Validate(
                options => options.OperationTimeoutSeconds is > 0 and <= 60,
                "Service Bus operation timeout must be between 1 and 60 seconds.")
            .Validate(
                options => options.ReceiveWaitSeconds is > 0 and <= 60,
                "Service Bus receive wait must be between 1 and 60 seconds.")
            .Validate(
                options => options.PrefetchCount is >= 0 and <= 100,
                "Service Bus prefetch must be between 0 and 100.")
            .ValidateOnStart();

        // Expose the bound ServiceBusOptions value as a resolvable service so the outbox dispatch
        // options can cross-validate against it via the Validate<TDep> overload below.
        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<ServiceBusOptions>>().Value);

        services.AddOptions<OutboxDispatchOptions>()
            .Bind(configuration.GetSection(OutboxDispatchOptions.SectionName))
            .Validate(
                options => options.BatchSize is > 0 and <= 100,
                "Outbox dispatch batch size must be between 1 and 100.")
            .Validate(
                options => options.PollIntervalSeconds is > 0 and <= 60,
                "Outbox dispatch poll interval must be between 1 and 60 seconds.")
            .Validate(
                options => options.LeaseDurationSeconds is > 0 and <= 300,
                "Outbox dispatch lease duration must be between 1 and 300 seconds.")
            .Validate<ServiceBusOptions>(
                (dispatch, serviceBus) => dispatch.LeaseDurationSeconds > serviceBus.OperationTimeoutSeconds,
                "Outbox dispatch lease duration must exceed the bounded Service Bus operation timeout.")
            .ValidateOnStart();

        services.AddSingleton<ServiceBusClient>(serviceProvider => ServiceBusClientFactory.Create(
            serviceProvider.GetRequiredService<IOptions<ServiceBusOptions>>().Value));
        services.AddSingleton<IContentProcessingPublisher, AzureServiceBusContentProcessingPublisher>();
        services.AddSingleton<IContentProcessingMessageReceiver, AzureServiceBusContentProcessingMessageReceiver>();
        services.AddScoped<IOutboxMessageStore, SqlOutboxMessageStore>();
        services.AddScoped<IContentProcessingInboxStore, SqlContentProcessingInboxStore>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        return services;
    }
}
