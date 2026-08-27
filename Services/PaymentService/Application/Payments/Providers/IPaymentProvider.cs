using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.Providers;

// Provider implementations never receive or persist card data. They create hosted/action sessions
// and report provider-signed lifecycle callbacks back through the webhook processor.
public interface IPaymentProvider
{
    string Name { get; }

    Task<PaymentProviderAction> CreateActionAsync(
        PaymentProviderActionRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentProviderWebhook?> RequestCaptureAsync(
        Payment payment,
        CancellationToken cancellationToken = default);

    Task<PaymentProviderWebhook?> RequestVoidAsync(
        Payment payment,
        CancellationToken cancellationToken = default);

    Task<PaymentProviderWebhook?> RequestRefundAsync(
        Payment payment,
        CancellationToken cancellationToken = default);
}

public interface ISandboxPaymentProvider : IPaymentProvider
{
    Task<PaymentProviderWebhook> CompleteAsync(
        Payment payment,
        SandboxPaymentOutcome outcome,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentProviderAction(
    string Provider,
    string SessionId,
    DateTime ExpiresAtUtc);

public sealed record PaymentProviderActionRequest(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Currency);

public sealed record PaymentProviderWebhook(string RawBody, string Signature);

public enum SandboxPaymentOutcome
{
    Approve,
    Decline
}
