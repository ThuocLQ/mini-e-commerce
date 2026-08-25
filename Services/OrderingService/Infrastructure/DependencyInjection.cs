using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MassTransit;
using BuildingBlocks.Contracts.Events.Orders;
using BuildingBlocks.Contracts.Events.Inventory;
using BuildingBlocks.Contracts.Events.Payments;
using OrderingService.Application.Abstractions;
using OrderingService.Application.IntegrationEvents;
using OrderingService.Infrastructure.Clients;
using OrderingService.Infrastructure.Messaging;
using OrderingService.Infrastructure.Outbox;
using OrderingService.Infrastructure.Sagas;
using OrderingService.Infrastructure.Persistence;

namespace OrderingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, PostgresDatabaseInitializer>();
        services.AddScoped<IOrderingUnitOfWork, DapperOrderingUnitOfWork>();
        services.AddScoped<IOrderRepository, DapperOrderRepository>();
        services.AddScoped<IOrderPaymentSagaRepository, DapperOrderPaymentSagaRepository>();
        services.AddScoped<IInboxRepository, DapperInboxRepository>();
        services.AddScoped<IOutboxRepository, DapperOutboxRepository>();
        services.AddPostgresReadinessCheck(configuration, "OrderingDb");
        services.AddRabbitMqReadinessCheck(configuration);

        var basketBaseUrl = configuration["ServiceUrls:BasketHttp"]
                            ?? throw new InvalidOperationException("ServiceUrls:BasketHttp is missing.");
        var catalogBaseUrl = configuration["ServiceUrls:CatalogHttp"]
                             ?? throw new InvalidOperationException("ServiceUrls:CatalogHttp is missing.");
        var discountBaseUrl = configuration["ServiceUrls:DiscountHttp"]
                              ?? throw new InvalidOperationException("ServiceUrls:DiscountHttp is missing.");
        var internalApiKey = configuration["InternalApi:Key"]
                             ?? throw new InvalidOperationException("InternalApi:Key is missing.");
        if (!environment.IsDevelopment() &&
            (internalApiKey.Contains("SET_BY_ENVIRONMENT", StringComparison.OrdinalIgnoreCase) || internalApiKey.Contains("CHANGEME", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("InternalApi:Key must be supplied from a non-development secret source outside Development.");
        }

        services.AddHttpClient<IBasketClient, HttpBasketClient>(client =>
        {
            client.BaseAddress = new Uri(basketBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        })
        .AddHttpMessageHandler<AccessTokenDelegatingHandler>();

        services.AddHttpClient<ICatalogProductSnapshotClient, HttpCatalogProductSnapshotClient>(client =>
        {
            client.BaseAddress = new Uri(catalogBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddHttpClient<IDiscountClient, HttpDiscountClient>(client =>
        {
            client.BaseAddress = new Uri(discountBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddHttpClient<IInventoryReservationClient, HttpInventoryReservationClient>(client =>
        {
            client.BaseAddress = new Uri(catalogBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.Add("X-MicroShop-Internal-Key", internalApiKey);
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

        services
            .AddOptions<KafkaOutboxOptions>()
            .Bind(configuration.GetSection(KafkaOutboxOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.BootstrapServers), "KafkaOutbox:BootstrapServers is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Topic), "KafkaOutbox:Topic is required.")
            .ValidateOnStart();

        services.AddMassTransit(busRegistrationConfigurator =>
        {
            busRegistrationConfigurator.AddConsumer<InventoryCommittedConsumer>();
            busRegistrationConfigurator.AddConsumer<InventoryReleasedConsumer>();
            busRegistrationConfigurator.UsingRabbitMq((context, busFactoryConfigurator) =>
            {
                var rabbitMqOptions = RabbitMqOptionsResolver.Resolve(configuration);

                busFactoryConfigurator.Message<OrderCreatedIntegrationEvent>(messageConfigurator =>
                {
                    messageConfigurator.SetEntityName("order.created");
                });

                busFactoryConfigurator.Message<OrderStatusChangedIntegrationEvent>(messageConfigurator =>
                {
                    messageConfigurator.SetEntityName("order.status-changed");
                });

                busFactoryConfigurator.Message<OrderPaymentSagaStateChangedIntegrationEvent>(messageConfigurator =>
                {
                    messageConfigurator.SetEntityName("order.payment-saga-state-changed");
                });
                busFactoryConfigurator.Message<InventoryCommitRequestedIntegrationEvent>(messageConfigurator => messageConfigurator.SetEntityName("inventory.commit-requested"));
                busFactoryConfigurator.Message<InventoryReleaseRequestedIntegrationEvent>(messageConfigurator => messageConfigurator.SetEntityName("inventory.release-requested"));
                busFactoryConfigurator.Message<InventoryCommittedIntegrationEvent>(messageConfigurator => messageConfigurator.SetEntityName("inventory.committed"));
                busFactoryConfigurator.Message<InventoryReleasedIntegrationEvent>(messageConfigurator => messageConfigurator.SetEntityName("inventory.released"));
                busFactoryConfigurator.Message<PaymentCaptureRequestedIntegrationEvent>(messageConfigurator => messageConfigurator.SetEntityName("payment.capture-requested"));

                busFactoryConfigurator.Host(
                    rabbitMqOptions.Host,
                    rabbitMqOptions.Port,
                    rabbitMqOptions.VirtualHost,
                    hostConfigurator =>
                    {
                        hostConfigurator.Username(rabbitMqOptions.UserName);
                        hostConfigurator.Password(rabbitMqOptions.Password);
                    });

                busFactoryConfigurator.ReceiveEndpoint("ordering.inventory-settlements", endpoint =>
                {
                    endpoint.ConfigureConsumer<InventoryCommittedConsumer>(context);
                    endpoint.ConfigureConsumer<InventoryReleasedConsumer>(context);
                });
            });
        });

        services.AddHostedService<OutboxPublisherBackgroundService>();
        services.AddHostedService<OutboxMetricsBackgroundService>();
        services.AddOptions<PaymentSagaTimeoutOptions>()
            .Bind(configuration.GetSection(PaymentSagaTimeoutOptions.SectionName))
            .Validate(options => options.IntervalSeconds > 0, "PaymentSagaTimeout:IntervalSeconds must be positive.")
            .Validate(options => options.BatchSize is > 0 and <= 1000, "PaymentSagaTimeout:BatchSize must be between 1 and 1000.")
            .ValidateOnStart();
        services.AddHostedService<PaymentSagaTimeoutWorker>();

        return services;
    }
}
