namespace OrderingService.Application.OrderPaymentSagas;

public sealed record OrderPaymentSagaDto(
    Guid Id,
    Guid OrderId,
    Guid PaymentId,
    string State,
    DateTime StartedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime TimeoutAtUtc,
    Guid? LastProcessedEventId,
    string? LastError);
