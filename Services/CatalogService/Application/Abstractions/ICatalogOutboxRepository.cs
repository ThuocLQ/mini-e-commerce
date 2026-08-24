using System.Data;
using CatalogService.Domain.Outbox;

namespace CatalogService.Application.Abstractions;

public interface ICatalogOutboxRepository
{
    Task AddAsync(CatalogOutboxMessage message, IDbTransaction transaction, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogOutboxMessage>> ClaimPendingAsync(
        int batchSize,
        int maxRetryCount,
        Guid lockId,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsProcessedAsync(
        Guid id,
        Guid lockId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsFailedAsync(
        Guid id,
        Guid lockId,
        string error,
        DateTime nextAttemptAtUtc,
        CancellationToken cancellationToken = default);
}
