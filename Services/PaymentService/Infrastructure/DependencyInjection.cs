using PaymentService.Application.Abstractions;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<IDatabaseInitializer, SqliteDatabaseInitializer>();
        services.AddScoped<IPaymentRepository, DapperPaymentRepository>();

        return services;
    }
}
