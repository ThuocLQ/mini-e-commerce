using System.Data;
using OrderingService.Application.Abstractions;
using OrderingService.Application.Orders.CancelOrder;
using OrderingService.Domain.OrderPaymentSagas;
using OrderingService.Domain.Orders;
using OrderingService.Domain.Outbox;

namespace MicroShop.IntegrationTests.Ordering;

public sealed class CancelOrderHandlerTests
{
    [Fact]
    public async Task PendingPaymentCancellation_ReleasesInventoryAndPromotion_AndIsIdempotent()
    {
        var order = CreateOrder(OrderStatus.PendingPayment, withDiscount: true);
        var saga = OrderPaymentSaga.Start(order.Id, Guid.NewGuid(), DateTime.UtcNow, TimeSpan.FromMinutes(30));
        var outbox = new RecordingOutboxRepository();
        var repository = new StubOrderRepository(order);
        var handler = new CancelOrderHandler(new InlineUnitOfWork(), repository, new StubSagaRepository(saga), outbox);
        var command = new CancelOrderCommand(order.Id, order.CustomerId, "Customer changed their mind.");

        var first = await handler.Handle(command, TestContext.Current.CancellationToken);
        var replay = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.NotNull(replay);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(1, repository.StatusUpdateCalls);
        Assert.Equal(OrderPaymentSagaState.OrderCancelled, saga.State);
        Assert.Equal(5, outbox.Messages.Count);
        Assert.Contains(outbox.Messages, message => message.Type.Contains("OrderStatusChangedIntegrationEvent"));
        Assert.Contains(outbox.Messages, message => message.Transport == OutboxTransport.Kafka && message.Type == "OrderCancelled");
        Assert.Contains(outbox.Messages, message => message.Type.Contains("InventoryReleaseRequestedIntegrationEvent"));
        Assert.Contains(outbox.Messages, message => message.Type.Contains("PromotionReleaseRequestedIntegrationEvent"));
        Assert.Contains(outbox.Messages, message => message.Type.Contains("OrderPaymentSagaStateChangedIntegrationEvent"));
    }

    [Fact]
    public async Task AuthorizedPaymentCancellation_RequestsVoidAndKeepsLateCaptureCompensable()
    {
        var order = CreateOrder(OrderStatus.PendingPayment);
        var saga = OrderPaymentSaga.Start(order.Id, Guid.NewGuid(), DateTime.UtcNow, TimeSpan.FromMinutes(30));
        saga.MarkPaymentAuthorized(Guid.NewGuid(), DateTime.UtcNow);
        var outbox = new RecordingOutboxRepository();
        var handler = new CancelOrderHandler(
            new InlineUnitOfWork(),
            new StubOrderRepository(order),
            new StubSagaRepository(saga),
            outbox);

        var result = await handler.Handle(
            new CancelOrderCommand(order.Id, order.CustomerId, "Customer requested cancellation."),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(OrderPaymentSagaState.VoidRequested, saga.State);
        Assert.Contains(outbox.Messages, message => message.Type.Contains("InventoryReleaseRequestedIntegrationEvent"));
        Assert.Contains(outbox.Messages, message => message.Type.Contains("PaymentVoidRequestedIntegrationEvent"));
    }

    [Fact]
    public async Task PaidOrderCancellation_IsRejectedWithoutWritingOutboxMessages()
    {
        var order = CreateOrder(OrderStatus.Paid);
        var outbox = new RecordingOutboxRepository();
        var handler = new CancelOrderHandler(
            new InlineUnitOfWork(),
            new StubOrderRepository(order),
            new StubSagaRepository(null),
            outbox);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CancelOrderCommand(order.Id, order.CustomerId, null),
            TestContext.Current.CancellationToken));

        Assert.Contains("refund or return workflow", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Empty(outbox.Messages);
    }

    private static Order CreateOrder(OrderStatus status, bool withDiscount = false)
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, status);
        order.AddItem(new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 10m, 1));
        if (withDiscount)
        {
            order.ApplyDiscount("SAVE10", 1m);
            order.AttachDiscountReservation(Guid.NewGuid());
        }

        return order;
    }

    private sealed class InlineUnitOfWork : IOrderingUnitOfWork
    {
        public Task<T> ExecuteAsync<T>(Func<IDbTransaction, Task<T>> operation, CancellationToken cancellationToken = default) =>
            operation(new StubTransaction());
    }

    private sealed class StubOrderRepository(Order order) : IOrderRepository
    {
        public int StatusUpdateCalls { get; private set; }

        public Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Order>>([order]);
        public Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Order>>(customerId == order.CustomerId ? [order] : []);
        public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(id == order.Id ? order : null);
        public Task<Order?> GetByIdAsync(Guid id, IDbTransaction transaction, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(id == order.Id ? order : null);
        public Task<Order?> GetByCustomerAndIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(null);
        public Task<Order?> GetByCustomerAndCheckoutBasketAsync(Guid customerId, Guid basketId, long basketVersion, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(null);
        public Task<Order> CreateAsync(Order createdOrder, IDbTransaction? transaction = null, CancellationToken cancellationToken = default) => Task.FromResult(createdOrder);

        public Task<bool> TryUpdateStatusAsync(Guid orderId, OrderStatus newStatus, IReadOnlyCollection<OrderStatus> expectedCurrentStatuses, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            StatusUpdateCalls++;
            return Task.FromResult(true);
        }
    }

    private sealed class StubSagaRepository(OrderPaymentSaga? saga) : IOrderPaymentSagaRepository
    {
        public Task<IReadOnlyList<OrderPaymentSaga>> GetTimedOutAsync(DateTime nowUtc, int batchSize, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<OrderPaymentSaga>>([]);
        public Task<OrderPaymentSaga?> GetByOrderIdAsync(Guid orderId, IDbTransaction transaction, CancellationToken cancellationToken = default) => Task.FromResult(orderId == saga?.OrderId ? saga : null);
        public Task UpsertAsync(OrderPaymentSaga savedSaga, IDbTransaction transaction, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingOutboxRepository : IOutboxRepository
    {
        public List<OutboxMessage> Messages { get; } = [];
        public Task AddAsync(OutboxMessage message, IDbTransaction? transaction = null, CancellationToken cancellationToken = default) { Messages.Add(message); return Task.CompletedTask; }
        public Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(int batchSize, int maxRetryCount, Guid lockId, DateTime nowUtc, TimeSpan lockDuration, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<OutboxMessage>>([]);
        public Task<IReadOnlyList<OutboxMessage>> GetLatestAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<OutboxMessage>>([]);
        public Task MarkAsProcessedAsync(Guid id, Guid lockId, DateTime processedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkAsFailedAsync(Guid id, Guid lockId, string error, DateTime nextAttemptAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubTransaction : IDbTransaction
    {
        public IDbConnection? Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() { }
        public void Dispose() { }
        public void Rollback() { }
    }
}