using CatalogService.Application.Abstractions;
using CatalogService.Infrastructure.Inventory;
using CatalogService.Infrastructure.Messaging;
using CatalogService.Infrastructure.Persistence;
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
        services.AddScoped<IInventoryReservationRepository, DapperInventoryReservationRepository>();
        services.AddHostedService<ExpiredInventoryReservationWorker>();
        services.AddPostgresReadinessCheck(configuration, "CatalogDb");
        services.AddRabbitMqReadinessCheck(configuration);

        var rabbitMqHost = configuration["RabbitMq:Host"] ?? throw new InvalidOperationException("RabbitMq:Host is missing.");
        var rabbitMqUserName = configuration["RabbitMq:UserName"] ?? throw new InvalidOperationException("RabbitMq:UserName is missing.");
        var rabbitMqPassword = configuration["RabbitMq:Password"] ?? throw new InvalidOperationException("RabbitMq:Password is missing.");
        var rabbitMqVirtualHost = configuration["RabbitMq:VirtualHost"] ?? "/";

        services.AddMassTransit(configurator =>
        {
            configurator.AddConsumer<InventoryCommitRequestedConsumer>();
            configurator.AddConsumer<InventoryReleaseRequestedConsumer>();
            configurator.UsingRabbitMq((context, bus) =>
            {
                bus.Message<InventoryCommitRequestedIntegrationEvent>(message =>
                    message.SetEntityName("inventory.commit-requested"));
                bus.Message<InventoryReleaseRequestedIntegrationEvent>(message =>
                    message.SetEntityName("inventory.release-requested"));

                bus.Host(rabbitMqHost, rabbitMqVirtualHost, host =>
                {
                    host.Username(rabbitMqUserName);
                    host.Password(rabbitMqPassword);
                });
                bus.ReceiveEndpoint("catalog.inventory-commands", endpoint =>
                {
                    endpoint.ConfigureConsumer<InventoryCommitRequestedConsumer>(context);
                    endpoint.ConfigureConsumer<InventoryReleaseRequestedConsumer>(context);
                });
            });
        });

        return services;
    }
}
