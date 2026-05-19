namespace PaymentService.Domain.Payments;

public sealed class Payment
{
    public Guid Id { get; }
    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    public decimal Amount { get; }
    public string Currency { get; }
    public PaymentStatus Status { get; private set; }
    public string? ProviderTransactionId { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAtUtc { get; }
    public DateTime? CompletedAtUtc { get; private set; }

    public Payment(
        Guid id,
        Guid orderId,
        Guid customerId,
        decimal amount,
        string currency,
        PaymentStatus status,
        DateTime createdAtUtc,
        string? providerTransactionId = null,
        string? failureReason = null,
        DateTime? completedAtUtc = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Payment id cannot be empty.", nameof(id));
        if (orderId == Guid.Empty) throw new ArgumentException("Order id cannot be empty.", nameof(orderId));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer id cannot be empty.", nameof(customerId));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency is required.", nameof(currency));

        Id = id;
        OrderId = orderId;
        CustomerId = customerId;
        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
        Status = status;
        ProviderTransactionId = string.IsNullOrWhiteSpace(providerTransactionId) ? null : providerTransactionId.Trim();
        FailureReason = string.IsNullOrWhiteSpace(failureReason) ? null : failureReason.Trim();
        CreatedAtUtc = createdAtUtc;
        CompletedAtUtc = completedAtUtc;
    }

    public void MarkSucceeded(string providerTransactionId, DateTime completedAtUtc)
    {
        if (Status == PaymentStatus.Succeeded)
        {
            return;
        }

        if (Status == PaymentStatus.Failed)
        {
            throw new InvalidOperationException("Failed payment cannot be marked as succeeded.");
        }

        if (string.IsNullOrWhiteSpace(providerTransactionId))
        {
            throw new ArgumentException("Provider transaction id is required.", nameof(providerTransactionId));
        }

        Status = PaymentStatus.Succeeded;
        ProviderTransactionId = providerTransactionId.Trim();
        FailureReason = null;
        CompletedAtUtc = completedAtUtc;
    }

    public void MarkFailed(string reason, DateTime completedAtUtc)
    {
        if (Status == PaymentStatus.Failed)
        {
            return;
        }

        if (Status == PaymentStatus.Succeeded)
        {
            throw new InvalidOperationException("Succeeded payment cannot be marked as failed.");
        }

        Status = PaymentStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Payment failed." : reason.Trim();
        CompletedAtUtc = completedAtUtc;
    }
}
