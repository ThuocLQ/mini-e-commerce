using BuildingBlocks.Contracts.Events.Inventory;
using InventoryService.Application.Abstractions;
using InventoryService.Infrastructure.Inventory;
using InventoryService.Infrastructure.Messaging;
using InventoryService.Infrastructure.Outbox;
using InventoryService.Infrastructure.Persistence;
using InventoryService.Infrastructure.Persistence.Outbox;
using MassTransit;

namespace InventoryService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, PostgresDatabaseInitializer>();
        services.AddScoped<IInventoryReservationRepository, DapperInventoryReservationRepository>();
        services.AddScoped<IInventoryItemRepository, DapperInventoryItemRepository>();
        services.AddScoped<IInventoryOutboxRepository, DapperInventoryOutboxRepository>();
        services.AddPostgresReadinessCheck(configuration, "InventoryDb");
        services.AddRabbitMqReadinessCheck(configuration);
        services.AddHostedService<ExpiredInventoryReservationWorker>();
        services.AddHostedService<InventoryOutboxPublisherBackgroundService>();

        services.AddOptions<InventoryOutboxPublisherOptions>()
            .Bind(configuration.GetSection(InventoryOutboxPublisherOptions.SectionName))
            .Validate(options => options.BatchSize is > 0 and <= 100, "InventoryOutboxPublisher:BatchSize must be between 1 and 100.")
            .Validate(options => options.IntervalSeconds > 0, "InventoryOutboxPublisher:IntervalSeconds must be positive.")
            .ValidateOnStart();

        var host = configuration["RabbitMq:Host"] ?? throw new InvalidOperationException("RabbitMq:Host is missing.");
        var userName = configuration["RabbitMq:UserName"] ?? throw new InvalidOperationException("RabbitMq:UserName is missing.");
        var password = configuration["RabbitMq:Password"] ?? throw new InvalidOperationException("RabbitMq:Password is missing.");
        var virtualHost = configuration["RabbitMq:VirtualHost"] ?? "/";

        services.AddMassTransit(configurator =>
        {
            configurator.AddConsumer<InventoryCommitRequestedConsumer>();
            configurator.AddConsumer<InventoryReleaseRequestedConsumer>();
            configurator.AddConsumer<InventoryItemProvisionRequestedConsumer>();
            configurator.UsingRabbitMq((context, bus) =>
            {
                bus.Message<InventoryCommitRequestedIntegrationEvent>(message => message.SetEntityName("inventory.commit-requested"));
                bus.Message<InventoryReleaseRequestedIntegrationEvent>(message => message.SetEntityName("inventory.release-requested"));
                bus.Message<InventoryCommittedIntegrationEvent>(message => message.SetEntityName("inventory.committed"));
                bus.Message<InventoryReleasedIntegrationEvent>(message => message.SetEntityName("inventory.released"));
                bus.Message<InventoryItemProvisionRequestedIntegrationEvent>(message => message.SetEntityName("inventory.item-provision-requested"));
                bus.Host(host, virtualHost, hostConfigurator =>
                {
                    hostConfigurator.Username(userName);
                    hostConfigurator.Password(password);
                });
                bus.ReceiveEndpoint("inventory.commands", endpoint =>
                {
                    endpoint.ConfigureConsumer<InventoryCommitRequestedConsumer>(context);
                    endpoint.ConfigureConsumer<InventoryReleaseRequestedConsumer>(context);
                    endpoint.ConfigureConsumer<InventoryItemProvisionRequestedConsumer>(context);
                });
            });
        });

        return services;
    }
}
