using System.Data;
using OrderingService.Application.Abstractions;
using OrderingService.Application.Orders.ApplyPaymentResult;
using OrderingService.Domain.Orders;
using OrderingService.Domain.Outbox;

namespace MicroShop.IntegrationTests.Ordering;

public sealed class ApplyPaymentResultOutboxTests
{
    [Fact]
    public async Task Succeeded_PersistsStatusAndProjectionOutboxMessages()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, OrderStatus.PendingPayment);
        order.AddItem(new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 12m, 1));
        var outbox = new RecordingOutboxRepository();
        var handler = new ApplyOrderPaymentResultHandler(
            new StubOrderRepository(order),
            outbox,
            new InlineUnitOfWork());

        var result = await handler.Handle(
            new ApplyOrderPaymentResultCommand(order.Id, OrderPaymentResult.Succeeded),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(nameof(OrderStatus.Paid), result.Status);
        Assert.Equal(2, outbox.Messages.Count);
        Assert.Contains(outbox.Messages, message => message.Transport == OutboxTransport.RabbitMq && message.Type.Contains("OrderStatusChangedIntegrationEvent"));
        Assert.Contains(outbox.Messages, message => message.Transport == OutboxTransport.Kafka && message.Type == "OrderPaid");
    }

    private sealed class InlineUnitOfWork : IOrderingUnitOfWork
    {
        public Task<T> ExecuteAsync<T>(Func<IDbTransaction, Task<T>> operation, CancellationToken cancellationToken = default) =>
            operation(new StubTransaction());
    }

    private sealed class StubOrderRepository(Order order) : IOrderRepository
    {
        public Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Order>>([order]);
        public Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Order>>([order]);
        public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(id == order.Id ? order : null);
        public Task<Order?> GetByIdAsync(Guid id, IDbTransaction transaction, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(id == order.Id ? order : null);
        public Task<Order?> GetByCustomerAndIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(null);
        public Task<Order> CreateAsync(Order createdOrder, IDbTransaction? transaction = null, CancellationToken cancellationToken = default) => Task.FromResult(createdOrder);
        public Task<bool> TryUpdateStatusAsync(Guid orderId, OrderStatus newStatus, IReadOnlyCollection<OrderStatus> expectedCurrentStatuses, IDbTransaction? transaction = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
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
        public void Rollback() { }
        public void Dispose() { }
    }
}
