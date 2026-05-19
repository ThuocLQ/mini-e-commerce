namespace PaymentService.Application.Payments;

public sealed record PaymentDto(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    string Status,
    string? ProviderTransactionId,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);