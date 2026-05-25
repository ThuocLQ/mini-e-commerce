using System.Data;
using OrderingService.Domain.Outbox;

namespace OrderingService.Application.Abstractions;

public interface IOutboxRepository
{
    Task AddAsync(
        OutboxMessage message,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
        int batchSize,
        int maxRetryCount,
        Guid lockId,
        DateTime nowUtc,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxMessage>> GetLatestAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task MarkAsProcessedAsync(
        Guid id,
        Guid lockId,
        DateTime processedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkAsFailedAsync(
        Guid id,
        Guid lockId,
        string error,
        DateTime nextAttemptAtUtc,
        CancellationToken cancellationToken = default);
}
