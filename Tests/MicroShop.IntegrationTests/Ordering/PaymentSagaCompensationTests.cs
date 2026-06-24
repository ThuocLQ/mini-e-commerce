using System.Data;
using OrderingService.Application.Abstractions;
using OrderingService.Application.OrderPaymentSagas.ApplyPaymentEvent;
using OrderingService.Domain.OrderPaymentSagas;
using OrderingService.Domain.Orders;

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
        var handler = new ApplyPaymentSagaEventHandler(
            new InlineUnitOfWork(),
            orderRepository,
            sagaRepository);

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
        Assert.Same(saga, sagaRepository.SavedSaga);
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

    private sealed class StubTransaction : IDbTransaction
    {
        public IDbConnection? Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() { }
        public void Dispose() { }
        public void Rollback() { }
    }
}
