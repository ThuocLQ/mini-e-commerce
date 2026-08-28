using Microsoft.Extensions.DependencyInjection;
using OrderingService.Application.Orders.CheckoutQuote;

namespace OrderingService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CheckoutAddressSnapshotResolver>();
        services.AddScoped<CheckoutQuoteEvaluator>();

        return services;
    }
}
