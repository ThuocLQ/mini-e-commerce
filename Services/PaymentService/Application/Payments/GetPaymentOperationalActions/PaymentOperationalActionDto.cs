namespace PaymentService.Application.Payments.GetPaymentOperationalActions;

public sealed record PaymentOperationalActionDto(
    Guid Id,
    string ActionType,
    string RequestedBy,
    string Reason,
    DateTime RequestedAtUtc,
    DateTime? CompletedAtUtc,
    string? FailureReason);