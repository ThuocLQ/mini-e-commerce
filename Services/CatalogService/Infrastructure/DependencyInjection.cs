using CatalogService.Application.Abstractions;
using CatalogService.Infrastructure.Persistence;
using CatalogService.Infrastructure.Persistence.Outbox;
using CatalogService.Infrastructure.Outbox;
using CatalogService.Infrastructure.Clients;
using BuildingBlocks.Contracts.Events.Inventory;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CatalogService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, PostgresDatabaseInitializer>();
        services.AddScoped<IProductRepository, DapperProductRepository>();
        services.AddScoped<ICatalogUnitOfWork, DapperCatalogUnitOfWork>();
        services.AddScoped<ICatalogOutboxRepository, DapperCatalogOutboxRepository>();
        services.AddHostedService<CatalogOutboxPublisherBackgroundService>();
        services.AddPostgresReadinessCheck(configuration, "CatalogDb");
        services.AddRabbitMqReadinessCheck(configuration);

        var inventoryBaseUrl = configuration["ServiceUrls:InventoryHttp"]
            ?? throw new InvalidOperationException("ServiceUrls:InventoryHttp is missing.");
        var internalApiKey = configuration["InternalApi:Key"]
            ?? throw new InvalidOperationException("InternalApi:Key is missing.");
        services.AddHttpClient<IInventoryStockClient, HttpInventoryStockClient>(client =>
        {
            client.BaseAddress = new Uri(inventoryBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.Add("X-MicroShop-Internal-Key", internalApiKey);
        });

        services
            .AddOptions<CatalogOutboxPublisherOptions>()
            .Bind(configuration.GetSection(CatalogOutboxPublisherOptions.SectionName))
            .Validate(options => options.BatchSize is > 0 and <= 100, "CatalogOutboxPublisher:BatchSize must be between 1 and 100.")
            .Validate(options => options.IntervalSeconds > 0, "CatalogOutboxPublisher:IntervalSeconds must be greater than 0.")
            .Validate(options => options.MaxRetryCount > 0, "CatalogOutboxPublisher:MaxRetryCount must be greater than 0.")
            .Validate(options => options.LockSeconds > 0, "CatalogOutboxPublisher:LockSeconds must be greater than 0.")
            .Validate(options => options.RetryDelaySeconds > 0, "CatalogOutboxPublisher:RetryDelaySeconds must be greater than 0.")
            .Validate(options => options.MaxRetryDelaySeconds >= options.RetryDelaySeconds, "CatalogOutboxPublisher:MaxRetryDelaySeconds must be greater than or equal to RetryDelaySeconds.")
            .ValidateOnStart();

        var rabbitMqHost = configuration["RabbitMq:Host"] ?? throw new InvalidOperationException("RabbitMq:Host is missing.");
        var rabbitMqUserName = configuration["RabbitMq:UserName"] ?? throw new InvalidOperationException("RabbitMq:UserName is missing.");
        var rabbitMqPassword = configuration["RabbitMq:Password"] ?? throw new InvalidOperationException("RabbitMq:Password is missing.");
        var rabbitMqVirtualHost = configuration["RabbitMq:VirtualHost"] ?? "/";

        services.AddMassTransit(configurator =>
        {
            configurator.UsingRabbitMq((context, bus) =>
            {
                bus.Message<InventoryItemProvisionRequestedIntegrationEvent>(message =>
                    message.SetEntityName("inventory.item-provision-requested"));

                bus.Host(rabbitMqHost, rabbitMqVirtualHost, host =>
                {
                    host.Username(rabbitMqUserName);
                    host.Password(rabbitMqPassword);
                });
            });
        });

        return services;
    }
}
