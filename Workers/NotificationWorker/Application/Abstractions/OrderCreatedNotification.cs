namespace NotificationWorker.Application.Abstractions;

public sealed record OrderCreatedNotification(
    Guid EventId,
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    string Currency,
    DateTime OccurredAtUtc,
    int Version,
    string? CorrelationId);
