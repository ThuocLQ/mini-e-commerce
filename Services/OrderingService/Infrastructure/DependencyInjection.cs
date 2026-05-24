using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MassTransit;
using BuildingBlocks.Contracts.Events.Orders;
using OrderingService.Application.Abstractions;
using OrderingService.Application.IntegrationEvents;
using OrderingService.Infrastructure.Clients;
using OrderingService.Infrastructure.Messaging;
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
        services.AddScoped<IOrderRepository, DapperOrderRepository>();

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

        return services;
    }
}
