using System.Data;
using InventoryService.Domain.Outbox;

namespace InventoryService.Application.Abstractions;

public interface IInventoryOutboxRepository
{
    Task AddAsync(InventoryOutboxMessage message, IDbTransaction transaction, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryOutboxMessage>> ClaimPendingAsync(
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

