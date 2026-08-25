using System.Data;
using OrderingService.Application.Abstractions;
using OrderingService.Application.OrderPaymentSagas.ApplyPaymentEvent;
using OrderingService.Domain.OrderPaymentSagas;
using OrderingService.Domain.Orders;
using OrderingService.Domain.Outbox;

namespace MicroShop.IntegrationTests.Ordering;

public sealed class PaymentSagaCompensationTests
{
    [Fact]
    public async Task PaymentSucceeded_AfterOrderCancelled_RequiresCompensation()
    {
        var now = DateTime.UtcNow;
        var order = new Order(
            Guid.NewGuid(),
            Guid.NewGuid(),
            now.AddMinutes(-5),
            OrderStatus.Cancelled);

        var paymentId = Guid.NewGuid();
        var saga = OrderPaymentSaga.Start(
            order.Id,
            paymentId,
            now.AddMinutes(-4),
            TimeSpan.FromMinutes(30));
        saga.MarkOrderCancelled(Guid.NewGuid(), now.AddMinutes(-3), "Payment failed.");

        var orderRepository = new StubOrderRepository(order);
        var sagaRepository = new StubSagaRepository(saga);
        var outboxRepository = new RecordingOutboxRepository();
        var handler = new ApplyPaymentSagaEventHandler(
            new InlineUnitOfWork(),
            orderRepository,
            sagaRepository,
            outboxRepository);

        var eventId = Guid.NewGuid();
        var result = await handler.Handle(
            new ApplyPaymentSagaEventCommand(
                eventId,
                OrderPaymentSagaEventType.PaymentSucceeded,
                order.Id,
                paymentId,
                null),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(nameof(OrderPaymentSagaState.CompensationRequired), result.State);
        Assert.Equal(eventId, result.LastProcessedEventId);
        Assert.Contains("cancelled or timed out", result.LastError);
        Assert.Equal(0, orderRepository.StatusUpdateCalls);
        var sagaEvent = Assert.Single(outboxRepository.Messages);
        Assert.Contains("OrderPaymentSagaStateChangedIntegrationEvent", sagaEvent.Type);
        Assert.Same(saga, sagaRepository.SavedSaga);
    }

    [Fact]
    public async Task PaymentSucceeded_PersistsRabbitAndKafkaTransitionEvents()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, OrderStatus.PendingPayment);
        order.AddItem(new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 10m, 1));
        var paymentId = Guid.NewGuid();
        var outboxRepository = new RecordingOutboxRepository();
        var handler = new ApplyPaymentSagaEventHandler(
            new InlineUnitOfWork(),
            new StubOrderRepository(order),
            new StubSagaRepository(OrderPaymentSaga.Start(order.Id, paymentId, DateTime.UtcNow, TimeSpan.FromMinutes(30))),
            outboxRepository);

        await handler.Handle(
            new ApplyPaymentSagaEventCommand(Guid.NewGuid(), OrderPaymentSagaEventType.PaymentSucceeded, order.Id, paymentId, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(4, outboxRepository.Messages.Count);
        Assert.Contains(outboxRepository.Messages, message =>
            message.Transport == OutboxTransport.RabbitMq &&
            message.Type.Contains("OrderStatusChangedIntegrationEvent"));
        Assert.Contains(outboxRepository.Messages, message =>
            message.Transport == OutboxTransport.RabbitMq &&
            message.Type.Contains("OrderPaymentSagaStateChangedIntegrationEvent"));
        Assert.Contains(outboxRepository.Messages, message =>
            message.Transport == OutboxTransport.Kafka && message.Type == "OrderPaid");
        Assert.Contains(outboxRepository.Messages, message =>
            message.Transport == OutboxTransport.RabbitMq &&
            message.Type.Contains("InventoryCommitRequestedIntegrationEvent"));
    }

    [Fact]
    public async Task PaymentFailed_PersistsInventoryReleaseCommandInTheOutbox()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, OrderStatus.PendingPayment);
        order.AddItem(new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 10m, 1));
        var paymentId = Guid.NewGuid();
        var outboxRepository = new RecordingOutboxRepository();
        var handler = new ApplyPaymentSagaEventHandler(
            new InlineUnitOfWork(),
            new StubOrderRepository(order),
            new StubSagaRepository(OrderPaymentSaga.Start(order.Id, paymentId, DateTime.UtcNow, TimeSpan.FromMinutes(30))),
            outboxRepository);

        await handler.Handle(
            new ApplyPaymentSagaEventCommand(Guid.NewGuid(), OrderPaymentSagaEventType.PaymentFailed, order.Id, paymentId, "Card declined."),
            TestContext.Current.CancellationToken);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(4, outboxRepository.Messages.Count);
        Assert.Contains(outboxRepository.Messages, message =>
            message.Transport == OutboxTransport.RabbitMq &&
            message.Type.Contains("InventoryReleaseRequestedIntegrationEvent"));
    }

    [Fact]
    public async Task PaymentTimedOut_PersistsInventoryReleaseCommandInTheOutbox()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, OrderStatus.PendingPayment);
        order.AddItem(new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 10m, 1));
        var paymentId = Guid.NewGuid();
        var outboxRepository = new RecordingOutboxRepository();
        var handler = new ApplyPaymentSagaEventHandler(
            new InlineUnitOfWork(),
            new StubOrderRepository(order),
            new StubSagaRepository(OrderPaymentSaga.Start(order.Id, paymentId, DateTime.UtcNow.AddMinutes(-31), TimeSpan.FromMinutes(30))),
            outboxRepository);

        await handler.Handle(
            new ApplyPaymentSagaEventCommand(Guid.NewGuid(), OrderPaymentSagaEventType.PaymentTimedOut, order.Id, paymentId, "Payment timed out."),
            TestContext.Current.CancellationToken);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Contains(outboxRepository.Messages, message =>
            message.Transport == OutboxTransport.RabbitMq &&
            message.Type.Contains("InventoryReleaseRequestedIntegrationEvent"));
    }

    [Fact]
    public async Task PaymentAuthorized_PersistsInventoryCommitWithoutMarkingOrderPaid()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, OrderStatus.PendingPayment);
        order.AddItem(new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 10m, 1));
        var paymentId = Guid.NewGuid();
        var saga = OrderPaymentSaga.Start(order.Id, paymentId, DateTime.UtcNow, TimeSpan.FromMinutes(30));
        var outboxRepository = new RecordingOutboxRepository();
        var handler = new ApplyPaymentSagaEventHandler(
            new InlineUnitOfWork(),
            new StubOrderRepository(order),
            new StubSagaRepository(saga),
            outboxRepository);

        var result = await handler.Handle(
            new ApplyPaymentSagaEventCommand(Guid.NewGuid(), OrderPaymentSagaEventType.PaymentAuthorized, order.Id, paymentId, null),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Equal(nameof(OrderPaymentSagaState.PaymentAuthorized), result.State);
        Assert.Equal(2, outboxRepository.Messages.Count);
        Assert.Contains(outboxRepository.Messages, message =>
            message.Transport == OutboxTransport.RabbitMq &&
            message.Type.Contains("InventoryCommitRequestedIntegrationEvent"));
    }

    [Fact]
    public async Task PaymentCaptured_AfterCaptureRequest_MarksOrderPaid()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, OrderStatus.PendingPayment);
        order.AddItem(new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 10m, 1));
        var paymentId = Guid.NewGuid();
        var saga = OrderPaymentSaga.Start(order.Id, paymentId, DateTime.UtcNow, TimeSpan.FromMinutes(30));
        saga.MarkPaymentAuthorized(Guid.NewGuid(), DateTime.UtcNow);
        saga.MarkCaptureRequested(Guid.NewGuid(), DateTime.UtcNow);
        var outboxRepository = new RecordingOutboxRepository();
        var handler = new ApplyPaymentSagaEventHandler(
            new InlineUnitOfWork(),
            new StubOrderRepository(order),
            new StubSagaRepository(saga),
            outboxRepository);

        var result = await handler.Handle(
            new ApplyPaymentSagaEventCommand(Guid.NewGuid(), OrderPaymentSagaEventType.PaymentCaptured, order.Id, paymentId, null),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(nameof(OrderPaymentSagaState.OrderPaid), result.State);
        Assert.Equal(3, outboxRepository.Messages.Count);
        Assert.Contains(outboxRepository.Messages, message => message.Transport == OutboxTransport.Kafka && message.Type == "OrderPaid");
    }

    [Fact]
    public async Task PaymentRefunded_AfterOrderPaid_MarksOrderRefunded()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, OrderStatus.PendingPayment);
        order.AddItem(new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 10m, 1));
        order.MarkPaid();
        var paymentId = Guid.NewGuid();
        var saga = OrderPaymentSaga.Start(order.Id, paymentId, DateTime.UtcNow, TimeSpan.FromMinutes(30));
        saga.MarkOrderPaid(Guid.NewGuid(), DateTime.UtcNow);
        var outboxRepository = new RecordingOutboxRepository();
        var handler = new ApplyPaymentSagaEventHandler(
            new InlineUnitOfWork(),
            new StubOrderRepository(order),
            new StubSagaRepository(saga),
            outboxRepository);

        var result = await handler.Handle(
            new ApplyPaymentSagaEventCommand(Guid.NewGuid(), OrderPaymentSagaEventType.PaymentRefunded, order.Id, paymentId, "Provider refund completed."),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Equal(nameof(OrderPaymentSagaState.OrderRefunded), result.State);
        Assert.Equal(3, outboxRepository.Messages.Count);
        Assert.Contains(outboxRepository.Messages, message => message.Transport == OutboxTransport.Kafka && message.Type == "OrderRefunded");
    }

    [Fact]
    public async Task PaymentTimeout_AfterAuthorization_RequestsVoidWithoutCancellingOrderEarly()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, OrderStatus.PendingPayment);
        order.AddItem(new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 10m, 1));
        var paymentId = Guid.NewGuid();
        var saga = OrderPaymentSaga.Start(order.Id, paymentId, DateTime.UtcNow.AddMinutes(-31), TimeSpan.FromMinutes(30));
        saga.MarkPaymentAuthorized(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-30));
        var outbox = new RecordingOutboxRepository();
        var handler = new ApplyPaymentSagaEventHandler(new InlineUnitOfWork(), new StubOrderRepository(order), new StubSagaRepository(saga), outbox);

        var result = await handler.Handle(
            new ApplyPaymentSagaEventCommand(Guid.NewGuid(), OrderPaymentSagaEventType.PaymentTimedOut, order.Id, paymentId, "Timed out."),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Equal(nameof(OrderPaymentSagaState.VoidRequested), result.State);
        Assert.Equal(2, outbox.Messages.Count);
        Assert.Contains(outbox.Messages, message => message.Type.Contains("PaymentVoidRequestedIntegrationEvent"));
    }

    [Fact]
    public async Task CapturedAfterVoidRequest_RequestsRefundThenCancelsAfterRefund()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, OrderStatus.PendingPayment);
        order.AddItem(new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Product", 10m, 1));
        var paymentId = Guid.NewGuid();
        var saga = OrderPaymentSaga.Start(order.Id, paymentId, DateTime.UtcNow, TimeSpan.FromMinutes(30));
        saga.MarkVoidRequested(Guid.NewGuid(), DateTime.UtcNow, "Timed out.");
        var outbox = new RecordingOutboxRepository();
        var handler = new ApplyPaymentSagaEventHandler(new InlineUnitOfWork(), new StubOrderRepository(order), new StubSagaRepository(saga), outbox);

        var captureResult = await handler.Handle(
            new ApplyPaymentSagaEventCommand(Guid.NewGuid(), OrderPaymentSagaEventType.PaymentCaptured, order.Id, paymentId, null),
            TestContext.Current.CancellationToken);

        Assert.NotNull(captureResult);
        Assert.Equal(nameof(OrderPaymentSagaState.RefundRequested), captureResult.State);
        Assert.Contains(outbox.Messages, message => message.Type.Contains("PaymentRefundRequestedIntegrationEvent"));

        var refundResult = await handler.Handle(
            new ApplyPaymentSagaEventCommand(Guid.NewGuid(), OrderPaymentSagaEventType.PaymentRefunded, order.Id, paymentId, "Refunded after timeout."),
            TestContext.Current.CancellationToken);

        Assert.NotNull(refundResult);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(nameof(OrderPaymentSagaState.CompensationCompleted), refundResult.State);
    }

    private sealed class InlineUnitOfWork : IOrderingUnitOfWork
    {
        public Task<T> ExecuteAsync<T>(
            Func<IDbTransaction, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            return operation(new StubTransaction());
        }
    }

    private sealed class StubOrderRepository(Order order) : IOrderRepository
    {
        public int StatusUpdateCalls { get; private set; }

        public Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Order>>([order]);

        public Task<IReadOnlyList<Order>> GetByCustomerAsync(
            Guid customerId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Order>>(
                customerId == order.CustomerId ? [order] : []);

        public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Order?>(id == order.Id ? order : null);

        public Task<Order?> GetByIdAsync(
            Guid id,
            IDbTransaction transaction,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Order?>(id == order.Id ? order : null);

        public Task<Order?> GetByCustomerAndIdempotencyKeyAsync(
            Guid customerId,
            string idempotencyKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Order?>(null);

        public Task<Order?> GetByCustomerAndCheckoutBasketAsync(
            Guid customerId,
            Guid basketId,
            long basketVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Order?>(null);

        public Task<Order> CreateAsync(
            Order createdOrder,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(createdOrder);

        public Task<bool> TryUpdateStatusAsync(
            Guid orderId,
            OrderStatus newStatus,
            IReadOnlyCollection<OrderStatus> expectedCurrentStatuses,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            StatusUpdateCalls++;
            return Task.FromResult(true);
        }
    }

    private sealed class StubSagaRepository(OrderPaymentSaga saga) : IOrderPaymentSagaRepository
    {
        public OrderPaymentSaga? SavedSaga { get; private set; }

        public Task<IReadOnlyList<OrderPaymentSaga>> GetTimedOutAsync(
            DateTime nowUtc,
            int batchSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OrderPaymentSaga>>(saga.TimeoutAtUtc <= nowUtc ? [saga] : []);

        public Task<OrderPaymentSaga?> GetByOrderIdAsync(
            Guid orderId,
            IDbTransaction transaction,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<OrderPaymentSaga?>(orderId == saga.OrderId ? saga : null);

        public Task UpsertAsync(
            OrderPaymentSaga savedSaga,
            IDbTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            SavedSaga = savedSaga;
            return Task.CompletedTask;
        }
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
