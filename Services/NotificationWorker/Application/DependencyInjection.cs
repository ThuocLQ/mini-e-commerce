using Microsoft.Extensions.DependencyInjection;
using NotificationWorker.Application.Notifications.HandleEmailVerification;
using NotificationWorker.Application.Notifications.HandleOrderCreated;
using NotificationWorker.Application.Notifications.HandleOrderStatusChanged;

namespace NotificationWorker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<OrderCreatedNotificationHandler>();
        services.AddScoped<OrderStatusChangedNotificationHandler>();
        services.AddScoped<EmailVerificationNotificationHandler>();
        return services;
    }
}