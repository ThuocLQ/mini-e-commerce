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
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default);

    Task<int> ReclaimExpiredLocksAsync(CancellationToken cancellationToken = default);

    Task<bool> MarkAsProcessedAsync(
        Guid messageId,
        Guid lockId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsFailedAsync(
        Guid messageId,
        Guid lockId,
        string error,
        DateTime nextAttemptAtUtc,
        CancellationToken cancellationToken = default);
}
