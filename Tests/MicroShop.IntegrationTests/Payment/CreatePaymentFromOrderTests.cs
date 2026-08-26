using System.Data;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Payments.CreatePayment;
using PaymentService.Domain.Payments;

namespace MicroShop.IntegrationTests.Payment;

public sealed class CreatePaymentFromOrderTests
{
    [Fact]
    public async Task CreatePayment_UsesAuthoritativeOrderSnapshot()
    {
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var repository = new StubPaymentRepository();
        var orderClient = new StubOrderPaymentClient(new OrderPaymentSnapshot(
            orderId,
            customerId,
            125_000m,
            "vnd",
            "PendingPayment"));
        var handler = new CreatePaymentHandler(repository, orderClient);

        var result = await handler.Handle(
            new CreatePaymentCommand(orderId, customerId),
            TestContext.Current.CancellationToken);

        Assert.Equal(orderId, result.OrderId);
        Assert.Equal(customerId, result.CustomerId);
        Assert.Equal(125_000m, result.Amount);
        Assert.Equal("VND", result.Currency);
        Assert.Equal(1, repository.CreateCalls);
    }

    [Fact]
    public async Task CreatePayment_ForExistingOrder_ReturnsExistingPayment()
    {
        var orderId = Guid.NewGuid();
        var existing = new PaymentService.Domain.Payments.Payment(
            Guid.NewGuid(),
            orderId,
            Guid.NewGuid(),
            75_000m,
            "USD",
            PaymentStatus.Pending,
            DateTime.UtcNow);
        var repository = new StubPaymentRepository(existing);
        var orderClient = new StubOrderPaymentClient(new OrderPaymentSnapshot(
            orderId,
            existing.CustomerId,
            existing.Amount,
            existing.Currency,
            "PendingPayment"));
        var handler = new CreatePaymentHandler(repository, orderClient);

        var result = await handler.Handle(
            new CreatePaymentCommand(orderId, existing.CustomerId),
            TestContext.Current.CancellationToken);

        Assert.Equal(existing.Id, result.Id);
        Assert.Equal(0, repository.CreateCalls);
        Assert.Equal(1, orderClient.CallCount);
    }

    [Fact]
    public async Task CreatePayment_ForAnotherCustomersOrder_IsNotAccessibleAndDoesNotCreatePayment()
    {
        var orderId = Guid.NewGuid();
        var repository = new StubPaymentRepository();
        var orderClient = new StubOrderPaymentClient(new OrderPaymentSnapshot(
            orderId,
            Guid.NewGuid(),
            125_000m,
            "VND",
            "PendingPayment"));
        var handler = new CreatePaymentHandler(repository, orderClient);

        await Assert.ThrowsAsync<PaymentOrderNotAccessibleException>(() => handler.Handle(
            new CreatePaymentCommand(orderId, Guid.NewGuid()),
            TestContext.Current.CancellationToken));

        Assert.Equal(0, repository.CreateCalls);
    }

    private sealed class StubOrderPaymentClient(OrderPaymentSnapshot? order) : IOrderPaymentClient
    {
        public int CallCount { get; private set; }

        public Task<OrderPaymentSnapshot?> GetOrderAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(order);
        }
    }

    private sealed class StubPaymentRepository(PaymentService.Domain.Payments.Payment? existing = null) : IPaymentRepository
    {
        private PaymentService.Domain.Payments.Payment? _payment = existing;

        public int CreateCalls { get; private set; }

        public Task<PaymentService.Domain.Payments.Payment> CreateAsync(
            PaymentService.Domain.Payments.Payment payment,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            _payment = payment;
            return Task.FromResult(payment);
        }

        public Task<PaymentService.Domain.Payments.Payment?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_payment?.Id == id ? _payment : null);

        public Task<PaymentService.Domain.Payments.Payment?> GetByIdAsync(
            Guid id,
            IDbTransaction transaction,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_payment?.Id == id ? _payment : null);

        public Task<PaymentService.Domain.Payments.Payment?> GetByOrderIdAsync(
            Guid orderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_payment?.OrderId == orderId ? _payment : null);

        public Task<IReadOnlyList<PaymentService.Domain.Payments.Payment>> GetRecentAsync(
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PaymentService.Domain.Payments.Payment>>(_payment is null ? [] : [_payment]);

        public Task<bool> UpdateAsync(
            PaymentService.Domain.Payments.Payment payment,
            CancellationToken cancellationToken = default)
        {
            _payment = payment;
            return Task.FromResult(true);
        }

        public Task<bool> UpdateAsync(
            PaymentService.Domain.Payments.Payment payment,
            IDbTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            _payment = payment;
            return Task.FromResult(true);
        }
    }
}
