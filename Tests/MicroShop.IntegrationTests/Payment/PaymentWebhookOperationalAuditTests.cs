using System.Data;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Payments.Webhooks;
using PaymentService.Domain.Payments;

namespace MicroShop.IntegrationTests.Payment;

public sealed class PaymentWebhookOperationalAuditTests
{
    [Fact]
    public async Task CapturedWebhook_CompletesPendingCaptureAction_Idempotently()
    {
        var payment = CreateCapturePendingPayment();
        var actions = new RecordingOperationalActionRepository(
        [
            PaymentOperationalAction.Create(
                payment.Id,
                "Capture",
                "OrderingSaga",
                "Capture requested by the order settlement saga.",
                DateTime.UtcNow.AddMinutes(-1))
        ]);
        var handler = new PaymentWebhookHandler(
            new CapturedPaymentWebhookRepository(payment),
            new NoopPaymentMetrics(),
            actions);
        var command = new PaymentWebhookCommand(
            payment.Id,
            "evt-capture-001",
            "provider-capture-001",
            "CAPTURED",
            null,
            "payload-hash",
            "Verified");

        await handler.Handle(command, TestContext.Current.CancellationToken);
        await handler.Handle(command, TestContext.Current.CancellationToken);

        var action = Assert.Single(actions.Actions);
        Assert.NotNull(action.CompletedAtUtc);
        Assert.Null(action.FailureReason);
        Assert.Equal(1, actions.SuccessfulCompletionCount);
    }

    private static PaymentService.Domain.Payments.Payment CreateCapturePendingPayment()
    {
        var payment = new PaymentService.Domain.Payments.Payment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 125_000m, "VND", PaymentStatus.PendingAuthorization, DateTime.UtcNow);
        payment.MarkAuthorized("provider-authorization-001", DateTime.UtcNow.AddMinutes(-1));
        payment.RequestCapture(DateTime.UtcNow);
        return payment;
    }

    private sealed class CapturedPaymentWebhookRepository(PaymentService.Domain.Payments.Payment payment) : IPaymentWebhookRepository
    {
        private bool _seen;

        public Task<PaymentWebhookApplyResult> ApplyAsync(
            string providerEventId,
            Guid paymentId,
            string providerTransactionId,
            PaymentStatus status,
            string? failureReason,
            string payloadHash,
            string signatureStatus,
            DateTime receivedAtUtc,
            CancellationToken cancellationToken = default)
        {
            var duplicate = _seen;
            _seen = true;
            if (!duplicate)
            {
                payment.MarkCaptured(providerTransactionId, receivedAtUtc);
            }

            return Task.FromResult(new PaymentWebhookApplyResult(payment, duplicate, providerEventId, status));
        }

        public Task RecordRejectedAsync(
            string providerEventId,
            Guid paymentId,
            string providerTransactionId,
            string eventType,
            string payloadHash,
            string signatureStatus,
            string error,
            DateTime receivedAtUtc,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingOperationalActionRepository(List<PaymentOperationalAction> actions) : IPaymentOperationalActionRepository
    {
        public List<PaymentOperationalAction> Actions { get; } = actions;
        public int SuccessfulCompletionCount { get; private set; }

        public Task CreateAsync(
            PaymentOperationalAction action,
            IDbTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }

        public Task CompleteLatestPendingAsync(
            Guid paymentId,
            string actionType,
            DateTime completedAtUtc,
            CancellationToken cancellationToken = default)
        {
            var index = Actions.FindLastIndex(action =>
                action.PaymentId == paymentId &&
                string.Equals(action.ActionType, actionType, StringComparison.OrdinalIgnoreCase) &&
                action.CompletedAtUtc is null);

            if (index >= 0)
            {
                Actions[index] = Actions[index] with { CompletedAtUtc = completedAtUtc, FailureReason = null };
                SuccessfulCompletionCount++;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PaymentOperationalAction>> GetByPaymentIdAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PaymentOperationalAction>>(
                Actions.Where(action => action.PaymentId == paymentId).ToList());
    }

    private sealed class NoopPaymentMetrics : IPaymentMetrics
    {
        public void RecordWebhookRequest(string outcome) { }
    }
}