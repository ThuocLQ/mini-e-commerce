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
        var expectedCommandEventId = Guid.NewGuid();
        saga.ExpectInventorySettlement(expectedCommandEventId);

        var first = await handler.Handle(
            new ApplyInventorySettlementEventCommand(
                eventId,
                OrderInventorySettlementEventType.InventoryCommitted,
                order.Id,
                null,
                expectedCommandEventId),
            TestContext.Current.CancellationToken);
        var replay = await handler.Handle(
            new ApplyInventorySettlementEventCommand(
                eventId,
                OrderInventorySettlementEventType.InventoryCommitted,
                order.Id,
                null,
                expectedCommandEventId),
            TestContext.Current.CancellationToken);

        Assert.True(first.OrderFound);
        Assert.NotNull(first.Saga);
        Assert.NotNull(replay.Saga);
        Assert.Equal(nameof(OrderPaymentSagaState.InventoryCommitted), first.Saga.State);
        Assert.Equal(nameof(OrderPaymentSagaState.InventoryCommitted), replay.Saga.State);
        Assert.Equal(eventId, first.Saga.LastProcessedEventId);
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

        Assert.True(result.OrderFound);
        Assert.NotNull(result.Saga);
        Assert.Equal(nameof(OrderPaymentSagaState.RefundRequested), result.Saga.State);
        Assert.Equal(eventId, result.Saga.LastProcessedEventId);
        Assert.Equal("ReservationExpired", result.Saga.LastError);
        Assert.Equal(2, outbox.Messages.Count);
        Assert.Contains(outbox.Messages, message => message.Type.Contains("PaymentRefundRequestedIntegrationEvent"));
    }

    [Fact]
    public async Task InventoryCommitted_AfterAuthorization_RequestsCaptureExactlyOnce()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-5), OrderStatus.PendingPayment, currency: "VND");
        order.AddItem(new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 125_000m, 1));
        var saga = OrderPaymentSaga.Start(order.Id, Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-4), TimeSpan.FromMinutes(30));
        saga.MarkPaymentAuthorized(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-3));
        var inbox = new RecordingInboxRepository();
        var outbox = new RecordingOutboxRepository();
        var handler = CreateHandler(order, saga, inbox, outbox);
        var eventId = Guid.NewGuid();
        var expectedCommandEventId = Guid.NewGuid();
        saga.ExpectInventorySettlement(expectedCommandEventId);

        var first = await handler.Handle(
            new ApplyInventorySettlementEventCommand(eventId, OrderInventorySettlementEventType.InventoryCommitted, order.Id, null, expectedCommandEventId),
            TestContext.Current.CancellationToken);
        var replay = await handler.Handle(
            new ApplyInventorySettlementEventCommand(eventId, OrderInventorySettlementEventType.InventoryCommitted, order.Id, null, expectedCommandEventId),
            TestContext.Current.CancellationToken);

        Assert.NotNull(first.Saga);
        Assert.NotNull(replay.Saga);
        Assert.Equal(nameof(OrderPaymentSagaState.CaptureRequested), first.Saga.State);
        Assert.Equal(nameof(OrderPaymentSagaState.CaptureRequested), replay.Saga.State);
        Assert.Equal(2, outbox.Messages.Count);
        Assert.Contains(outbox.Messages, message => message.Type.Contains("PaymentCaptureRequestedIntegrationEvent"));
        Assert.Contains(outbox.Messages, message => message.Type.Contains("OrderPaymentSagaStateChangedIntegrationEvent"));
    }

    [Fact]
    public async Task InventoryCommitted_WithUnexpectedCausation_IsIgnored()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-5), OrderStatus.PendingPayment);
        order.AddItem(new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 10m, 1));
        var saga = OrderPaymentSaga.Start(order.Id, Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-4), TimeSpan.FromMinutes(30));
        saga.MarkPaymentAuthorized(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-3));
        saga.ExpectInventorySettlement(Guid.NewGuid());
        var outbox = new RecordingOutboxRepository();
        var handler = CreateHandler(order, saga, new RecordingInboxRepository(), outbox);

        var result = await handler.Handle(
            new ApplyInventorySettlementEventCommand(
                Guid.NewGuid(),
                OrderInventorySettlementEventType.InventoryCommitted,
                order.Id,
                null,
                Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result.Saga);
        Assert.Equal(nameof(OrderPaymentSagaState.PaymentAuthorized), result.Saga.State);
        Assert.Contains("causation", result.Saga.LastError, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(outbox.Messages);
    }

    [Fact]
    public async Task InventoryReleased_WithoutPaymentSaga_CancelsPendingOrderAndWritesAuditsOnce()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-31), OrderStatus.PendingPayment);
        order.AddItem(new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 10m, 1));
        var outbox = new RecordingOutboxRepository();
        var repository = new StubOrderRepository(order);
        var handler = new ApplyInventorySettlementEventHandler(
            new InlineUnitOfWork(),
            repository,
            new StubSagaRepository(null),
            new RecordingInboxRepository(),
            outbox);
        var eventId = Guid.NewGuid();
        var command = new ApplyInventorySettlementEventCommand(
            eventId,
            OrderInventorySettlementEventType.InventoryReleased,
            order.Id,
            "ReservationExpired");

        var first = await handler.Handle(command, TestContext.Current.CancellationToken);
        var replay = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(first.OrderFound);
        Assert.Null(first.Saga);
        Assert.True(replay.OrderFound);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(1, repository.StatusUpdateCalls);
        Assert.Equal(2, outbox.Messages.Count);
        Assert.Contains(outbox.Messages, message => message.Type.Contains("OrderStatusChangedIntegrationEvent") && message.CausationId == eventId.ToString("D"));
        Assert.Contains(outbox.Messages, message => message.Transport == OutboxTransport.Kafka && message.Type == "OrderCancelled");
    }

    private static ApplyInventorySettlementEventHandler CreateHandler(
        Order order,
        OrderPaymentSaga? saga,
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
        public int StatusUpdateCalls { get; private set; }
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

        public Task<bool> TryUpdateStatusAsync(Guid orderId, OrderStatus newStatus, IReadOnlyCollection<OrderStatus> expectedCurrentStatuses, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            StatusUpdateCalls++;
            return Task.FromResult(true);
        }
    }

    private sealed class StubSagaRepository(OrderPaymentSaga? saga) : IOrderPaymentSagaRepository
    {
        public Task<IReadOnlyList<OrderPaymentSaga>> GetTimedOutAsync(DateTime nowUtc, int batchSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OrderPaymentSaga>>([]);

        public Task<OrderPaymentSaga?> GetByOrderIdAsync(Guid orderId, IDbTransaction transaction, CancellationToken cancellationToken = default) =>
            Task.FromResult<OrderPaymentSaga?>(saga is not null && orderId == saga.OrderId ? saga : null);

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
