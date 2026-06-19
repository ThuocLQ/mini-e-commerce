using PaymentService.Application.Abstractions;
using PaymentService.Infrastructure.Outbox;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, PostgresDatabaseInitializer>();
        services.AddScoped<IPaymentRepository, DapperPaymentRepository>();
        services.AddScoped<IPaymentWebhookRepository, DapperPaymentWebhookRepository>();
        services.AddScoped<IPaymentOutboxRepository, DapperPaymentOutboxRepository>();

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

        return services;
    }
}
