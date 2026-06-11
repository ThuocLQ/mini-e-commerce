using PaymentService.Application.Abstractions;
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

        return services;
    }
}
