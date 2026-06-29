using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MassTransit;
using BuildingBlocks.Contracts.Events.Orders;
using OrderingService.Application.Abstractions;
using OrderingService.Application.IntegrationEvents;
using OrderingService.Infrastructure.Clients;
using OrderingService.Infrastructure.Messaging;
using OrderingService.Infrastructure.Outbox;
using OrderingService.Infrastructure.Persistence;

namespace OrderingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, PostgresDatabaseInitializer>();
        services.AddScoped<IOrderingUnitOfWork, DapperOrderingUnitOfWork>();
        services.AddScoped<IOrderRepository, DapperOrderRepository>();
        services.AddScoped<IOrderPaymentSagaRepository, DapperOrderPaymentSagaRepository>();
        services.AddScoped<IOutboxRepository, DapperOutboxRepository>();
        services.AddPostgresReadinessCheck(configuration, "OrderingDb");
        services.AddRabbitMqReadinessCheck(configuration);

        var basketBaseUrl = configuration["ServiceUrls:BasketHttp"]
                            ?? throw new InvalidOperationException("ServiceUrls:BasketHttp is missing.");

        services.AddHttpClient<IBasketClient, HttpBasketClient>(client =>
        {
            client.BaseAddress = new Uri(basketBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        
        services
            .AddOptions<OrderEventOptions>()
            .Bind(configuration.GetSection(OrderEventOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Currency), "OrderEvents:Currency is required.")
            .ValidateOnStart();

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
            .AddOptions<OutboxPublisherOptions>()
            .Bind(configuration.GetSection(OutboxPublisherOptions.SectionName))
            .Validate(options => options.BatchSize > 0 && options.BatchSize <= 100, "OutboxPublisher:BatchSize must be between 1 and 100.")
            .Validate(options => options.IntervalSeconds > 0, "OutboxPublisher:IntervalSeconds must be greater than 0.")
            .Validate(options => options.MaxRetryCount > 0, "OutboxPublisher:MaxRetryCount must be greater than 0.")
            .Validate(options => options.LockSeconds > 0, "OutboxPublisher:LockSeconds must be greater than 0.")
            .Validate(options => options.RetryDelaySeconds > 0, "OutboxPublisher:RetryDelaySeconds must be greater than 0.")
            .Validate(options => options.MaxRetryDelaySeconds >= options.RetryDelaySeconds, "OutboxPublisher:MaxRetryDelaySeconds must be greater than or equal to RetryDelaySeconds.")
            .ValidateOnStart();

        services.AddMassTransit(busRegistrationConfigurator =>
        {
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
            });
        });

        services.AddHostedService<OutboxPublisherBackgroundService>();
        services.AddHostedService<OutboxMetricsBackgroundService>();

        return services;
    }
}
