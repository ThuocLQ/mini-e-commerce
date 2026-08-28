using System.Data;
using OrderingService.Application.Abstractions;
using OrderingService.Application.Orders.AdvanceFulfillment;
using OrderingService.Domain.Orders;
using OrderingService.Domain.Outbox;

namespace MicroShop.IntegrationTests.Ordering;

public sealed class AdvanceFulfillmentHandlerTests
{
    [Fact]
    public async Task PaidOrder_TransitionsToConfirmed_AndReplayDoesNotAdvanceAgain()
    {
        var order = CreateOrder(OrderStatus.Paid);
        var repository = new StubOrderRepository(order);
        var outbox = new RecordingOutboxRepository();
        var handler = new AdvanceFulfillmentHandler(new InlineUnitOfWork(), repository, outbox);
        var command = new AdvanceFulfillmentCommand(order.Id, OrderStatus.Confirmed);

        var first = await handler.Handle(command, TestContext.Current.CancellationToken);
        var replay = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.NotNull(replay);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(1, repository.StatusUpdateCalls);
        Assert.Equal(2, outbox.Messages.Count);
        Assert.Contains(outbox.Messages, message => message.Type.Contains("OrderStatusChangedIntegrationEvent"));
        Assert.Contains(outbox.Messages, message => message.Transport == OutboxTransport.Kafka && message.Type == "OrderConfirmed");
    }

    [Fact]
    public async Task PaidOrder_CannotSkipDirectlyToShipped()
    {
        var order = CreateOrder(OrderStatus.Paid);
        var outbox = new RecordingOutboxRepository();
        var handler = new AdvanceFulfillmentHandler(new InlineUnitOfWork(), new StubOrderRepository(order), outbox);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new AdvanceFulfillmentCommand(order.Id, OrderStatus.Shipped),
            TestContext.Current.CancellationToken));

        Assert.Contains("fulfillment workflow", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Empty(outbox.Messages);
    }

    [Fact]
    public void FulfillmentTransitions_AreSequential_AndTerminalDeliveryCannotBeRepeatedThroughAnotherStatus()
    {
        var order = CreateOrder(OrderStatus.Paid);

        Assert.True(order.MoveToFulfillmentStatus(OrderStatus.Confirmed));
        Assert.True(order.MoveToFulfillmentStatus(OrderStatus.Shipped));
        Assert.True(order.MoveToFulfillmentStatus(OrderStatus.Delivered));
        Assert.False(order.MoveToFulfillmentStatus(OrderStatus.Delivered));
        Assert.Throws<InvalidOperationException>(() => order.MoveToFulfillmentStatus(OrderStatus.Shipped));
    }

    private static Order CreateOrder(OrderStatus status)
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, status);
        order.AddItem(new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 10m, 1));
        return order;
    }

    private sealed class InlineUnitOfWork : IOrderingUnitOfWork
    {
        public Task<T> ExecuteAsync<T>(Func<IDbTransaction, Task<T>> operation, CancellationToken cancellationToken = default) => operation(new StubTransaction());
    }

    private sealed class StubOrderRepository(Order order) : IOrderRepository
    {
        public int StatusUpdateCalls { get; private set; }
        public Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Order>>([order]);
        public Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Order>>([]);
        public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(id == order.Id ? order : null);
        public Task<Order?> GetByIdAsync(Guid id, IDbTransaction transaction, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(id == order.Id ? order : null);
        public Task<Order?> GetByCustomerAndIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(null);
        public Task<Order?> GetByCustomerAndCheckoutBasketAsync(Guid customerId, Guid basketId, long basketVersion, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(null);
        public Task<Order> CreateAsync(Order createdOrder, IDbTransaction? transaction = null, CancellationToken cancellationToken = default) => Task.FromResult(createdOrder);
        public Task<bool> TryUpdateStatusAsync(Guid orderId, OrderStatus newStatus, IReadOnlyCollection<OrderStatus> expectedCurrentStatuses, IDbTransaction? transaction = null, CancellationToken cancellationToken = default) { StatusUpdateCalls++; return Task.FromResult(true); }
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