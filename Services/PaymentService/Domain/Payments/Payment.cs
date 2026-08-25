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
    public DateTime? AuthorizedAtUtc { get; private set; }
    public DateTime? CaptureRequestedAtUtc { get; private set; }
    public DateTime? CapturedAtUtc { get; private set; }
    public DateTime? VoidRequestedAtUtc { get; private set; }
    public DateTime? VoidedAtUtc { get; private set; }
    public DateTime? RefundRequestedAtUtc { get; private set; }
    public DateTime? RefundedAtUtc { get; private set; }

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
        DateTime? completedAtUtc = null,
        DateTime? authorizedAtUtc = null,
        DateTime? captureRequestedAtUtc = null,
        DateTime? capturedAtUtc = null,
        DateTime? voidRequestedAtUtc = null,
        DateTime? voidedAtUtc = null,
        DateTime? refundRequestedAtUtc = null,
        DateTime? refundedAtUtc = null)
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
        AuthorizedAtUtc = authorizedAtUtc;
        CaptureRequestedAtUtc = captureRequestedAtUtc;
        CapturedAtUtc = capturedAtUtc;
        VoidRequestedAtUtc = voidRequestedAtUtc;
        VoidedAtUtc = voidedAtUtc;
        RefundRequestedAtUtc = refundRequestedAtUtc;
        RefundedAtUtc = refundedAtUtc;
    }

    public void MarkAuthorized(string providerTransactionId, DateTime authorizedAtUtc)
    {
        if (Status == PaymentStatus.Authorized)
        {
            return;
        }

        if (Status != PaymentStatus.PendingAuthorization)
        {
            throw new InvalidOperationException($"Payment in status '{Status}' cannot be authorized.");
        }

        SetProviderTransactionId(providerTransactionId);
        Status = PaymentStatus.Authorized;
        FailureReason = null;
        AuthorizedAtUtc = authorizedAtUtc;
    }

    public void RequestCapture(DateTime requestedAtUtc)
    {
        if (Status is PaymentStatus.CapturePending or PaymentStatus.Captured)
        {
            return;
        }

        if (Status != PaymentStatus.Authorized)
        {
            throw new InvalidOperationException($"Payment in status '{Status}' cannot be captured.");
        }

        Status = PaymentStatus.CapturePending;
        CaptureRequestedAtUtc = requestedAtUtc;
    }

    public void MarkCaptured(string providerTransactionId, DateTime capturedAtUtc)
    {
        if (Status == PaymentStatus.Captured)
        {
            return;
        }

        if (Status != PaymentStatus.CapturePending)
        {
            throw new InvalidOperationException($"Payment in status '{Status}' cannot be captured.");
        }

        SetProviderTransactionId(providerTransactionId);
        Status = PaymentStatus.Captured;
        FailureReason = null;
        CapturedAtUtc = capturedAtUtc;
        CompletedAtUtc = capturedAtUtc;
    }

    public void RequestVoid(DateTime requestedAtUtc)
    {
        if (Status is PaymentStatus.VoidPending or PaymentStatus.Voided)
        {
            return;
        }

        if (Status is not (PaymentStatus.Authorized or PaymentStatus.CapturePending))
        {
            throw new InvalidOperationException($"Payment in status '{Status}' cannot be voided.");
        }

        Status = PaymentStatus.VoidPending;
        VoidRequestedAtUtc = requestedAtUtc;
    }

    public void MarkVoided(DateTime voidedAtUtc)
    {
        if (Status == PaymentStatus.Voided)
        {
            return;
        }

        if (Status != PaymentStatus.VoidPending)
        {
            throw new InvalidOperationException($"Payment in status '{Status}' cannot be marked as voided.");
        }

        Status = PaymentStatus.Voided;
        FailureReason = null;
        VoidedAtUtc = voidedAtUtc;
        CompletedAtUtc = voidedAtUtc;
    }

    public void RequestRefund(DateTime requestedAtUtc)
    {
        if (Status is PaymentStatus.RefundPending or PaymentStatus.Refunded)
        {
            return;
        }

        if (Status != PaymentStatus.Captured)
        {
            throw new InvalidOperationException($"Payment in status '{Status}' cannot be refunded.");
        }

        Status = PaymentStatus.RefundPending;
        RefundRequestedAtUtc = requestedAtUtc;
    }

    public void MarkRefunded(DateTime refundedAtUtc)
    {
        if (Status == PaymentStatus.Refunded)
        {
            return;
        }

        if (Status != PaymentStatus.RefundPending)
        {
            throw new InvalidOperationException($"Payment in status '{Status}' cannot be marked as refunded.");
        }

        Status = PaymentStatus.Refunded;
        FailureReason = null;
        RefundedAtUtc = refundedAtUtc;
        CompletedAtUtc = refundedAtUtc;
    }

    // Compatibility entry point for the existing SUCCEEDED webhook.
    public void MarkSucceeded(string providerTransactionId, DateTime completedAtUtc)
    {
        MarkCaptured(providerTransactionId, completedAtUtc);
    }

    public void MarkFailed(string reason, DateTime completedAtUtc)
    {
        if (Status == PaymentStatus.Failed)
        {
            return;
        }

        if (Status != PaymentStatus.PendingAuthorization)
        {
            throw new InvalidOperationException($"Payment in status '{Status}' cannot be marked as failed.");
        }

        Status = PaymentStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Payment failed." : reason.Trim();
        CompletedAtUtc = completedAtUtc;
    }

    private void SetProviderTransactionId(string providerTransactionId)
    {
        if (string.IsNullOrWhiteSpace(providerTransactionId))
        {
            throw new ArgumentException("Provider transaction id is required.", nameof(providerTransactionId));
        }

        ProviderTransactionId = providerTransactionId.Trim();
    }
}
