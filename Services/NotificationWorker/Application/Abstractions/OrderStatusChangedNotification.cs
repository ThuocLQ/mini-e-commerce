namespace NotificationWorker.Application.Abstractions;

public sealed record OrderStatusChangedNotification(
    Guid EventId,
    Guid OrderId,
    Guid CustomerId,
    string PreviousStatus,
    string CurrentStatus,
    decimal TotalAmount,
    string Currency,
    DateTime OccurredAtUtc,
    int Version,
    string? CorrelationId);