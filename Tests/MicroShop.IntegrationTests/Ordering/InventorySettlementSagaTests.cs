using System.Data;
using OrderingService.Application.Abstractions;
using OrderingService.Application.OrderPaymentSagas.ApplyInventorySettlement;
using OrderingService.Domain.OrderPaymentSagas;
using OrderingService.Domain.Orders;
using OrderingService.Domain.Outbox;

namespace MicroShop.IntegrationTests.Ordering;

public sealed class InventorySettlementSagaTests
{
    [Fact]
    public async Task InventoryCommitted_TransitionsPaidSagaOnce_AndWritesOneStateChange()
    {
        var order = CreatePaidOrder();
        var saga = CreatePaidSaga(order.Id);
        var inbox = new RecordingInboxRepository();
        var outbox = new RecordingOutboxRepository();
        var handler = CreateHandler(order, saga, inbox, outbox);
        var eventId = Guid.NewGuid();

        var first = await handler.Handle(
            new ApplyInventorySettlementEventCommand(
                eventId,
                OrderInventorySettlementEventType.InventoryCommitted,
                order.Id,
                null),
            TestContext.Current.CancellationToken);
        var replay = await handler.Handle(
            new ApplyInventorySettlementEventCommand(
                eventId,
                OrderInventorySettlementEventType.InventoryCommitted,
                order.Id,
                null),
            TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.NotNull(replay);
        Assert.Equal(nameof(OrderPaymentSagaState.InventoryCommitted), first.State);
        Assert.Equal(nameof(OrderPaymentSagaState.InventoryCommitted), replay.State);
        Assert.Equal(eventId, first.LastProcessedEventId);
        var stateChanged = Assert.Single(outbox.Messages);
        Assert.Contains("OrderPaymentSagaStateChangedIntegrationEvent", stateChanged.Type);
        Assert.Equal(eventId.ToString("D"), stateChanged.CausationId);
    }

    [Fact]
    public async Task InventoryReleased_AfterPaid_RequiresCompensation()
    {
        var order = CreatePaidOrder();
        var saga = CreatePaidSaga(order.Id);
        var outbox = new RecordingOutboxRepository();
        var handler = CreateHandler(order, saga, new RecordingInboxRepository(), outbox);
        var eventId = Guid.NewGuid();

        var result = await handler.Handle(
            new ApplyInventorySettlementEventCommand(
                eventId,
                OrderInventorySettlementEventType.InventoryReleased,
                order.Id,
                "ReservationExpired"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(nameof(OrderPaymentSagaState.CompensationRequired), result.State);
        Assert.Equal(eventId, result.LastProcessedEventId);
        Assert.Equal("ReservationExpired", result.LastError);
        Assert.Single(outbox.Messages);
    }

    private static ApplyInventorySettlementEventHandler CreateHandler(
        Order order,
        OrderPaymentSaga saga,
        RecordingInboxRepository inbox,
        RecordingOutboxRepository outbox)
    {
        return new ApplyInventorySettlementEventHandler(
            new InlineUnitOfWork(),
            new StubOrderRepository(order),
            new StubSagaRepository(saga),
            inbox,
            outbox);
    }

    private static Order CreatePaidOrder()
    {
        return new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-5), OrderStatus.Paid);
    }

    private static OrderPaymentSaga CreatePaidSaga(Guid orderId)
    {
        var saga = OrderPaymentSaga.Start(orderId, Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-4), TimeSpan.FromMinutes(30));
        saga.MarkOrderPaid(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-3));
        return saga;
    }

    private sealed class InlineUnitOfWork : IOrderingUnitOfWork
    {
        public Task<T> ExecuteAsync<T>(Func<IDbTransaction, Task<T>> operation, CancellationToken cancellationToken = default) =>
            operation(new StubTransaction());
    }

    private sealed class StubOrderRepository(Order order) : IOrderRepository
    {
        public Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Order>>([order]);

        public Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Order>>(customerId == order.CustomerId ? [order] : []);

        public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Order?>(id == order.Id ? order : null);

        public Task<Order?> GetByIdAsync(Guid id, IDbTransaction transaction, CancellationToken cancellationToken = default) =>
            Task.FromResult<Order?>(id == order.Id ? order : null);

        public Task<Order?> GetByCustomerAndIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Order?>(null);

        public Task<Order?> GetByCustomerAndCheckoutBasketAsync(Guid customerId, Guid basketId, long basketVersion, CancellationToken cancellationToken = default) =>
            Task.FromResult<Order?>(null);

        public Task<Order> CreateAsync(Order createdOrder, IDbTransaction? transaction = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(createdOrder);

        public Task<bool> TryUpdateStatusAsync(Guid orderId, OrderStatus newStatus, IReadOnlyCollection<OrderStatus> expectedCurrentStatuses, IDbTransaction? transaction = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class StubSagaRepository(OrderPaymentSaga saga) : IOrderPaymentSagaRepository
    {
        public Task<IReadOnlyList<OrderPaymentSaga>> GetTimedOutAsync(DateTime nowUtc, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OrderPaymentSaga>>([]);

        public Task<OrderPaymentSaga?> GetByOrderIdAsync(Guid orderId, IDbTransaction transaction, CancellationToken cancellationToken = default) =>
            Task.FromResult<OrderPaymentSaga?>(orderId == saga.OrderId ? saga : null);

        public Task UpsertAsync(OrderPaymentSaga savedSaga, IDbTransaction transaction, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingInboxRepository : IInboxRepository
    {
        private readonly HashSet<(string ConsumerName, Guid EventId)> _messages = [];

        public Task<bool> TryRecordAsync(Guid eventId, string consumerName, IDbTransaction transaction, CancellationToken cancellationToken = default) =>
            Task.FromResult(_messages.Add((consumerName, eventId)));
    }

    private sealed class RecordingOutboxRepository : IOutboxRepository
    {
        public List<OutboxMessage> Messages { get; } = [];

        public Task AddAsync(OutboxMessage message, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(int batchSize, int maxRetryCount, Guid lockId, DateTime nowUtc, TimeSpan lockDuration, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OutboxMessage>>([]);

        public Task<IReadOnlyList<OutboxMessage>> GetLatestAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OutboxMessage>>([]);

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
