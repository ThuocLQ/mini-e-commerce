using BuildingBlocks.Contracts.Events.Orders;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationWorker.Application.Abstractions;
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
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "RabbitMq:Host is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.VirtualHost), "RabbitMq:VirtualHost is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.UserName), "RabbitMq:UserName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Password), "RabbitMq:Password is required.")
            .ValidateOnStart();

        services.AddScoped<INotificationSender, LoggingNotificationSender>();

        services.AddMassTransit(busRegistrationConfigurator =>
        {
            busRegistrationConfigurator.AddConsumer<OrderCreatedIntegrationEventConsumer>();

            busRegistrationConfigurator.UsingRabbitMq((context, busFactoryConfigurator) =>
            {
                var rabbitMqOptions = configuration
                    .GetSection(RabbitMqOptions.SectionName)
                    .Get<RabbitMqOptions>()
                    ?? new RabbitMqOptions();

                busFactoryConfigurator.Message<OrderCreatedIntegrationEvent>(messageConfigurator =>
                {
                    messageConfigurator.SetEntityName("order.created");
                });

                busFactoryConfigurator.Host(
                    rabbitMqOptions.Host,
                    rabbitMqOptions.VirtualHost,
                    hostConfigurator =>
                    {
                        hostConfigurator.Username(rabbitMqOptions.UserName);
                        hostConfigurator.Password(rabbitMqOptions.Password);
                    });

                busFactoryConfigurator.ReceiveEndpoint("notification.order-created", endpointConfigurator =>
                {
                    endpointConfigurator.ConfigureConsumer<OrderCreatedIntegrationEventConsumer>(context);
                });
            });
        });

        return services;
    }
}
