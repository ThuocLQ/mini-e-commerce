using Microsoft.Extensions.Logging.Abstractions;
using NotificationWorker.Application.Abstractions;
using NotificationWorker.Application.Notifications;
using NotificationWorker.Application.Notifications.HandleOrderStatusChanged;

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

    private static OrderStatusChangedNotificationHandler CreateHandler(CapturingNotificationSender sender)
    {
        var processor = new NotificationDeliveryProcessor(
            new InMemoryNotificationDeliveryStore(),
            NullLogger<NotificationDeliveryProcessor>.Instance);

        return new OrderStatusChangedNotificationHandler(processor, sender);
    }

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

    private sealed class InMemoryNotificationDeliveryStore : INotificationDeliveryStore
    {
        private readonly Dictionary<string, DeliveryState> _deliveries = [];

        public Task<NotificationDeliveryLeaseAcquisition> TryStartAsync(
            NotificationDelivery delivery,
            CancellationToken cancellationToken = default)
        {
            var key = $"{delivery.EventId:D}:{delivery.Template}:{delivery.Channel}";
            if (!_deliveries.TryGetValue(key, out var state))
            {
                state = new DeliveryState(Guid.NewGuid());
                _deliveries[key] = state;
            }

            if (state.Sent)
            {
                return Task.FromResult(new NotificationDeliveryLeaseAcquisition(NotificationDeliveryStartResult.AlreadySent));
            }

            if (state.Processing)
            {
                return Task.FromResult(new NotificationDeliveryLeaseAcquisition(NotificationDeliveryStartResult.AlreadyProcessing));
            }

            state.Processing = true;
            state.LeaseToken = Guid.NewGuid();
            return Task.FromResult(new NotificationDeliveryLeaseAcquisition(
                NotificationDeliveryStartResult.Started,
                state.DeliveryId,
                state.LeaseToken));
        }

        public Task<bool> MarkSentAsync(Guid deliveryId, Guid leaseToken, CancellationToken cancellationToken = default)
        {
            var state = _deliveries.Values.SingleOrDefault(candidate => candidate.DeliveryId == deliveryId);
            if (state is null || !state.Processing || state.LeaseToken != leaseToken)
            {
                return Task.FromResult(false);
            }

            state.Processing = false;
            state.Sent = true;
            return Task.FromResult(true);
        }

        public Task<bool> MarkRetryableFailureAsync(
            Guid deliveryId,
            Guid leaseToken,
            Exception exception,
            CancellationToken cancellationToken = default)
        {
            var state = _deliveries.Values.SingleOrDefault(candidate => candidate.DeliveryId == deliveryId);
            if (state is null || !state.Processing || state.LeaseToken != leaseToken)
            {
                return Task.FromResult(false);
            }

            state.Processing = false;
            return Task.FromResult(true);
        }

        public Task<int> MarkExhaustedAsDeadLetterAsync(
            int maxAttempts,
            DateTime olderThanUtc,
            CancellationToken cancellationToken = default) => Task.FromResult(0);

        private sealed class DeliveryState(Guid deliveryId)
        {
            public Guid DeliveryId { get; } = deliveryId;
            public Guid LeaseToken { get; set; }
            public bool Processing { get; set; }
            public bool Sent { get; set; }
        }
    }
}