using PaymentService.Application.Abstractions;
using PaymentService.Application.Payments.Webhooks;
using PaymentService.Infrastructure.Observability;
using PaymentService.Infrastructure.Outbox;
using PaymentService.Infrastructure.Persistence;
using PaymentService.Infrastructure.Clients;
using BuildingBlocks.Contracts.Events.Payments;
using MassTransit;
using PaymentService.Infrastructure.Messaging;
using PaymentService.Infrastructure.Providers;
using PaymentService.Application.Payments.Providers;

namespace PaymentService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, PostgresDatabaseInitializer>();
        services.AddScoped<IPaymentUnitOfWork, DapperPaymentUnitOfWork>();
        services.AddScoped<IPaymentRepository, DapperPaymentRepository>();
        services.AddScoped<IPaymentInboxRepository, DapperPaymentInboxRepository>();
        services.AddScoped<IPaymentWebhookRepository, DapperPaymentWebhookRepository>();
        services.AddScoped<IPaymentOutboxRepository, DapperPaymentOutboxRepository>();
        services.AddSingleton<IPaymentMetrics, PaymentMetrics>();
        services.AddPostgresReadinessCheck(configuration, "PaymentDb");
        services.AddRabbitMqReadinessCheck(configuration);

        var providerKind = configuration[$"{PaymentProviderOptions.SectionName}:Provider"]?.Trim();
        if (IsSandboxEnvironment(environment))
        {
            if (!string.Equals(providerKind, "Sandbox", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Development/Portfolio payment runs must explicitly configure PaymentProvider:Provider=Sandbox. " +
                    "No commercial provider adapter is bundled with this service.");
            }

            services.AddSingleton<SandboxPaymentProvider>();
            services.AddSingleton<IPaymentProvider>(serviceProvider => serviceProvider.GetRequiredService<SandboxPaymentProvider>());
            services.AddSingleton<ISandboxPaymentProvider>(serviceProvider => serviceProvider.GetRequiredService<SandboxPaymentProvider>());
        }
        else
        {
            throw new InvalidOperationException(
                "A commercial payment provider adapter must be configured outside Development/Portfolio. " +
                "The sandbox provider is intentionally unavailable in this environment.");
        }

        var orderingBaseUrl = configuration["ServiceUrls:OrderingHttp"]
                              ?? throw new InvalidOperationException("ServiceUrls:OrderingHttp is missing.");

        services.AddHttpClient<IOrderPaymentClient, HttpOrderPaymentClient>(client =>
        {
            client.BaseAddress = new Uri(orderingBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        })
        .AddHttpMessageHandler<AccessTokenDelegatingHandler>();

        services
            .AddOptions<PaymentWebhookOptions>()
            .Bind(configuration.GetSection(PaymentWebhookOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.SignatureHeaderName),
                "PaymentWebhooks:SignatureHeaderName is required.")
            .Validate(
                options => !options.RequireSignature || !string.IsNullOrWhiteSpace(options.SharedSecret),
                "PaymentWebhooks:SharedSecret is required when signatures are enabled.")
            .Validate(
                options => environment.IsDevelopment() || IsProductionWebhookSecret(options.SharedSecret),
                "PaymentWebhooks:SharedSecret must be supplied through a production secret source and must not use a development placeholder.")
            .ValidateOnStart();

        services
            .AddOptions<PaymentOutboxDispatcherOptions>()
            .Bind(configuration.GetSection(PaymentOutboxDispatcherOptions.SectionName))
            .Validate(options => options.BatchSize > 0 && options.BatchSize <= 100, "PaymentOutboxDispatcher:BatchSize must be between 1 and 100.")
            .Validate(options => options.IntervalSeconds > 0, "PaymentOutboxDispatcher:IntervalSeconds must be greater than 0.")
            .Validate(options => options.MaxRetryCount > 0, "PaymentOutboxDispatcher:MaxRetryCount must be greater than 0.")
            .Validate(options => options.LockSeconds > 0, "PaymentOutboxDispatcher:LockSeconds must be greater than 0.")
            .Validate(options => options.RetryDelaySeconds > 0, "PaymentOutboxDispatcher:RetryDelaySeconds must be greater than 0.")
            .Validate(options => options.MaxRetryDelaySeconds >= options.RetryDelaySeconds, "PaymentOutboxDispatcher:MaxRetryDelaySeconds must be greater than or equal to RetryDelaySeconds.")
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
            .AddOptions<OrderingSagaClientOptions>()
            .Bind(configuration.GetSection(OrderingSagaClientOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.OrderingHttp), "ServiceUrls:OrderingHttp is required.")
            .ValidateOnStart();

        services.AddHttpClient<OrderingPaymentSagaClient>(client =>
        {
            client.BaseAddress = new Uri(orderingBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddMassTransit(busRegistrationConfigurator =>
        {
            busRegistrationConfigurator.AddConsumer<PaymentCaptureRequestedConsumer>();
            busRegistrationConfigurator.AddConsumer<PaymentVoidRequestedConsumer>();
            busRegistrationConfigurator.AddConsumer<PaymentRefundRequestedConsumer>();
            busRegistrationConfigurator.UsingRabbitMq((context, busFactoryConfigurator) =>
            {
                var rabbitMqOptions = RabbitMqOptionsResolver.Resolve(configuration);

                busFactoryConfigurator.Message<PaymentCaptureRequestedIntegrationEvent>(messageConfigurator =>
                {
                    messageConfigurator.SetEntityName("payment.capture-requested");
                });
                busFactoryConfigurator.Message<PaymentVoidRequestedIntegrationEvent>(messageConfigurator =>
                {
                    messageConfigurator.SetEntityName("payment.void-requested");
                });
                busFactoryConfigurator.Message<PaymentRefundRequestedIntegrationEvent>(messageConfigurator =>
                {
                    messageConfigurator.SetEntityName("payment.refund-requested");
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

                busFactoryConfigurator.ReceiveEndpoint("payment.capture-requests", endpoint =>
                {
                    endpoint.ConfigureConsumer<PaymentCaptureRequestedConsumer>(context);
                });
                busFactoryConfigurator.ReceiveEndpoint("payment.void-requests", endpoint =>
                {
                    endpoint.ConfigureConsumer<PaymentVoidRequestedConsumer>(context);
                });
                busFactoryConfigurator.ReceiveEndpoint("payment.refund-requests", endpoint =>
                {
                    endpoint.ConfigureConsumer<PaymentRefundRequestedConsumer>(context);
                });
            });
        });

        services.AddHostedService<PaymentOutboxDispatcherBackgroundService>();
        services.AddHostedService<PaymentOutboxMetricsBackgroundService>();

        return services;
    }

    private static bool IsProductionWebhookSecret(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            return false;
        }

        return !secret.StartsWith("SET_BY_", StringComparison.OrdinalIgnoreCase)
               && !secret.Contains("dev-webhook-secret", StringComparison.OrdinalIgnoreCase)
               && !secret.Contains("CHANGEME", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSandboxEnvironment(IHostEnvironment environment) =>
        environment.IsDevelopment() ||
        string.Equals(environment.EnvironmentName, "Portfolio", StringComparison.OrdinalIgnoreCase);
}
