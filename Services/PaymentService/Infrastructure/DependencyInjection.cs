using PaymentService.Application.Abstractions;
using PaymentService.Application.Payments.Webhooks;
using PaymentService.Infrastructure.Observability;
using PaymentService.Infrastructure.Outbox;
using PaymentService.Infrastructure.Persistence;

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
        services.AddScoped<IPaymentRepository, DapperPaymentRepository>();
        services.AddScoped<IPaymentWebhookRepository, DapperPaymentWebhookRepository>();
        services.AddScoped<IPaymentOutboxRepository, DapperPaymentOutboxRepository>();
        services.AddSingleton<IPaymentMetrics, PaymentMetrics>();
        services.AddPostgresReadinessCheck(configuration, "PaymentDb");

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
            .AddOptions<OrderingSagaClientOptions>()
            .Bind(configuration.GetSection(OrderingSagaClientOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.OrderingHttp), "ServiceUrls:OrderingHttp is required.")
            .ValidateOnStart();

        var orderingBaseUrl = configuration["ServiceUrls:OrderingHttp"]
                              ?? throw new InvalidOperationException("ServiceUrls:OrderingHttp is missing.");

        services.AddHttpClient<OrderingPaymentSagaClient>(client =>
        {
            client.BaseAddress = new Uri(orderingBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
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
}
