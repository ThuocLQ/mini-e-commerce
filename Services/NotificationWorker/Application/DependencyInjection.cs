using Microsoft.Extensions.DependencyInjection;
using NotificationWorker.Application.Notifications.HandleEmailVerification;
using NotificationWorker.Application.Notifications.HandleOrderCreated;
using NotificationWorker.Application.Notifications.HandleOrderStatusChanged;
using NotificationWorker.Application.Notifications;

namespace NotificationWorker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<NotificationDeliveryProcessor>();
        services.AddScoped<OrderCreatedNotificationHandler>();
        services.AddScoped<OrderStatusChangedNotificationHandler>();
        services.AddScoped<EmailVerificationNotificationHandler>();
        return services;
    }
}