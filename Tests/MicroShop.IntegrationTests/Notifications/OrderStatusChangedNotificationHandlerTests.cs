using Microsoft.Extensions.Logging.Abstractions;
using NotificationWorker.Application.Abstractions;
using NotificationWorker.Application.Notifications.HandleOrderStatusChanged;
using NotificationWorker.Infrastructure.Idempotency;

namespace MicroShop.IntegrationTests.Notifications;

public sealed class OrderStatusChangedNotificationHandlerTests
{
    [Fact]
    public async Task DuplicateEvent_IsDeliveredOnlyOnce()
    {
        var sender = new CapturingNotificationSender();
        var handler = CreateHandler(sender);
        var notification = CreateNotification();

        await handler.HandleAsync(notification, TestContext.Current.CancellationToken);
        await handler.HandleAsync(notification, TestContext.Current.CancellationToken);

        Assert.Single(sender.StatusChangedNotifications);
        Assert.Equal("Paid", sender.StatusChangedNotifications[0].CurrentStatus);
    }

    [Fact]
    public async Task SenderFailure_ReleasesLeaseSoRetryCanDeliver()
    {
        var sender = new CapturingNotificationSender { FailStatusChangedDelivery = true };
        var handler = CreateHandler(sender);
        var notification = CreateNotification();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(notification, TestContext.Current.CancellationToken));

        sender.FailStatusChangedDelivery = false;
        await handler.HandleAsync(notification, TestContext.Current.CancellationToken);

        Assert.Single(sender.StatusChangedNotifications);
    }

    private static OrderStatusChangedNotificationHandler CreateHandler(CapturingNotificationSender sender) =>
        new(
            new InMemoryProcessedEventStore(),
            sender,
            NullLogger<OrderStatusChangedNotificationHandler>.Instance);

    private static OrderStatusChangedNotification CreateNotification() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "PendingPayment",
        "Paid",
        79m,
        "USD",
        DateTime.UtcNow,
        1,
        "test-correlation");

    private sealed class CapturingNotificationSender : INotificationSender
    {
        public List<OrderStatusChangedNotification> StatusChangedNotifications { get; } = [];
        public bool FailStatusChangedDelivery { get; set; }

        public Task SendOrderCreatedAsync(OrderCreatedNotification notification, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendEmailVerificationAsync(EmailVerificationNotification notification, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendOrderStatusChangedAsync(OrderStatusChangedNotification notification, CancellationToken cancellationToken)
        {
            if (FailStatusChangedDelivery)
            {
                throw new InvalidOperationException("Configured sender failure.");
            }

            StatusChangedNotifications.Add(notification);
            return Task.CompletedTask;
        }
    }
}