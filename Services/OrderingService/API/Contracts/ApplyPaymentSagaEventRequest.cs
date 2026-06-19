namespace OrderingService.API.Contracts;

public sealed record ApplyPaymentSagaEventRequest(
    Guid EventId,
    Guid PaymentId,
    string EventType,
    string? FailureReason);
