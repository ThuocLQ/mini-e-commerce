using BuildingBlocks.Contracts.Events.Orders;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationWorker.Application.Abstractions;
using NotificationWorker.Infrastructure.Idempotency;
using NotificationWorker.Infrastructure.Messaging;
using NotificationWorker.Infrastructure.Notifications;

namespace NotificationWorker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<RabbitMqOptions>()
            .Configure(options =>
            {
                var resolvedOptions = RabbitMqOptionsResolver.Resolve(configuration);
                options.Host = resolvedOptions.Host;
                options.Port = resolvedOptions.Port;
                options.VirtualHost = resolvedOptions.VirtualHost;
                options.UserName = resolvedOptions.UserName;
                options.Password = resolvedOptions.Password;
            })
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "RabbitMq:Host is required.")
            .Validate(options => options.Port > 0, "RabbitMq:Port is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.VirtualHost), "RabbitMq:VirtualHost is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.UserName), "RabbitMq:UserName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Password), "RabbitMq:Password is required.")
            .ValidateOnStart();

        services
            .AddOptions<MessageRetryOptions>()
            .Bind(configuration.GetSection(MessageRetryOptions.SectionName))
            .Validate(options => options.RetryCount >= 0, "Messaging:Retry:RetryCount cannot be negative.")
            .Validate(options => options.IntervalSeconds > 0, "Messaging:Retry:IntervalSeconds must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton<IProcessedEventStore, InMemoryProcessedEventStore>();
        services.AddScoped<INotificationSender, LoggingNotificationSender>();

        services.AddMassTransit(busRegistrationConfigurator =>
        {
            busRegistrationConfigurator.AddConsumer<OrderCreatedIntegrationEventConsumer>();

            busRegistrationConfigurator.UsingRabbitMq((context, busFactoryConfigurator) =>
            {
                var rabbitMqOptions = RabbitMqOptionsResolver.Resolve(configuration);

                busFactoryConfigurator.Message<OrderCreatedIntegrationEvent>(messageConfigurator =>
                {
                    messageConfigurator.SetEntityName("order.created");
                });

                busFactoryConfigurator.Host(
                    rabbitMqOptions.Host,
                    rabbitMqOptions.Port,
                    rabbitMqOptions.VirtualHost,
                    hostConfigurator =>
                    {
                        hostConfigurator.Username(rabbitMqOptions.UserName);
                        hostConfigurator.Password(rabbitMqOptions.Password);
                    });

                busFactoryConfigurator.ReceiveEndpoint("notification.order-created", endpointConfigurator =>
                {
                    var retryOptions = configuration
                        .GetSection(MessageRetryOptions.SectionName)
                        .Get<MessageRetryOptions>()
                        ?? new MessageRetryOptions();

                    endpointConfigurator.UseMessageRetry(retryConfigurator =>
                    {
                        retryConfigurator.Ignore<ArgumentException>();
                        retryConfigurator.Interval(
                            retryOptions.RetryCount,
                            TimeSpan.FromSeconds(retryOptions.IntervalSeconds));
                    });

                    endpointConfigurator.ConfigureConsumer<OrderCreatedIntegrationEventConsumer>(context);
                });
            });
        });

        return services;
    }
}
