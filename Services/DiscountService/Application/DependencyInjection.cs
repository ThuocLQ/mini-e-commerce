using DiscountService.Domain.Discounts;

namespace DiscountService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        services.AddSingleton<IDiscountStrategy, PercentageDiscountStrategy>();
        services.AddSingleton<IDiscountStrategy, FixedAmountDiscountStrategy>();
        services.AddSingleton<DiscountStrategyFactory>();

        return services;
    }
}
