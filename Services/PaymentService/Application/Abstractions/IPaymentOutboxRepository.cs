using System.Data;
using PaymentService.Domain.Outbox;

namespace PaymentService.Application.Abstractions;

public interface IPaymentOutboxRepository
{
    Task AddAsync(
        PaymentOutboxMessage message,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentOutboxMessage>> ClaimPendingAsync(
        int batchSize,
        int maxRetryCount,
        Guid lockId,
        DateTime nowUtc,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default);

    Task MarkAsProcessedAsync(
        Guid messageId,
        Guid lockId,
        DateTime processedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkAsFailedAsync(
        Guid messageId,
        Guid lockId,
        string error,
        DateTime nextAttemptAtUtc,
        CancellationToken cancellationToken = default);
}
