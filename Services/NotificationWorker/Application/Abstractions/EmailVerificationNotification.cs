namespace NotificationWorker.Application.Abstractions;

public sealed record EmailVerificationNotification(
    Guid EventId,
    Guid CustomerId,
    string Token,
    DateTime ExpiresAtUtc,
    string? CorrelationId);