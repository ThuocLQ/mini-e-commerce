using Microsoft.Extensions.DependencyInjection;
using NotificationWorker.Application.Notifications.HandleOrderCreated;

namespace NotificationWorker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<OrderCreatedNotificationHandler>();

        return services;
    }
}
