using System.Data;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Payments.RequestCapture;
using PaymentService.Domain.Payments;

namespace MicroShop.IntegrationTests.Payment;

public sealed class PaymentCaptureRequestTests
{
    [Fact]
    public async Task CaptureRequest_IsAppliedOnceAndRecordedInInbox()
    {
        var payment = CreateAuthorizedPayment();
        var repository = new StubPaymentRepository(payment);
        var handler = new RequestPaymentCaptureHandler(
            new InlineUnitOfWork(),
            repository,
            new RecordingInboxRepository());
        var command = new RequestPaymentCaptureCommand(
            Guid.NewGuid(),
            payment.Id,
            payment.OrderId,
            payment.CustomerId,
            payment.Amount,
            payment.Currency,
            DateTime.UtcNow);

        var first = await handler.Handle(command, TestContext.Current.CancellationToken);
        var replay = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(first.CaptureWasRequested);
        Assert.False(first.WasAlreadyProcessed);
        Assert.True(replay.WasAlreadyProcessed);
        Assert.False(replay.CaptureWasRequested);
        Assert.Equal(PaymentStatus.CapturePending, payment.Status);
        Assert.Equal(1, repository.TransactionalUpdateCalls);
    }

    [Fact]
    public async Task CaptureRequest_WithDifferentAmount_IsRejectedBeforeInboxWrite()
    {
        var payment = CreateAuthorizedPayment();
        var inbox = new RecordingInboxRepository();
        var handler = new RequestPaymentCaptureHandler(
            new InlineUnitOfWork(),
            new StubPaymentRepository(payment),
            inbox);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new RequestPaymentCaptureCommand(
                Guid.NewGuid(),
                payment.Id,
                payment.OrderId,
                payment.CustomerId,
                payment.Amount + 1m,
                payment.Currency,
                DateTime.UtcNow),
            TestContext.Current.CancellationToken));

        Assert.Empty(inbox.EventIds);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
    }

    private static PaymentService.Domain.Payments.Payment CreateAuthorizedPayment()
    {
        var payment = new PaymentService.Domain.Payments.Payment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 125_000m, "VND", PaymentStatus.PendingAuthorization, DateTime.UtcNow);
        payment.MarkAuthorized("provider-transaction-001", DateTime.UtcNow);
        return payment;
    }

    private sealed class InlineUnitOfWork : IPaymentUnitOfWork
    {
        public Task<T> ExecuteAsync<T>(Func<IDbTransaction, Task<T>> operation, CancellationToken cancellationToken = default) =>
            operation(new StubTransaction());
    }

    private sealed class StubPaymentRepository(PaymentService.Domain.Payments.Payment payment) : IPaymentRepository
    {
        public int TransactionalUpdateCalls { get; private set; }

        public Task<PaymentService.Domain.Payments.Payment> CreateAsync(PaymentService.Domain.Payments.Payment createdPayment, CancellationToken cancellationToken = default) =>
            Task.FromResult(createdPayment);

        public Task<PaymentService.Domain.Payments.Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<PaymentService.Domain.Payments.Payment?>(id == payment.Id ? payment : null);

        public Task<PaymentService.Domain.Payments.Payment?> GetByIdAsync(Guid id, IDbTransaction transaction, CancellationToken cancellationToken = default) =>
            Task.FromResult<PaymentService.Domain.Payments.Payment?>(id == payment.Id ? payment : null);

        public Task<PaymentService.Domain.Payments.Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult<PaymentService.Domain.Payments.Payment?>(orderId == payment.OrderId ? payment : null);

        public Task<bool> UpdateAsync(PaymentService.Domain.Payments.Payment updatedPayment, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> UpdateAsync(PaymentService.Domain.Payments.Payment updatedPayment, IDbTransaction transaction, CancellationToken cancellationToken = default)
        {
            TransactionalUpdateCalls++;
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingInboxRepository : IPaymentInboxRepository
    {
        public HashSet<Guid> EventIds { get; } = [];

        public Task<bool> TryRecordAsync(Guid eventId, string consumerName, IDbTransaction transaction, CancellationToken cancellationToken = default) =>
            Task.FromResult(EventIds.Add(eventId));
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
