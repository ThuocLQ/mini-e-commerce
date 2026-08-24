using System.Data;

namespace OrderingService.Application.Abstractions;

public interface IInboxRepository
{
    Task<bool> TryRecordAsync(
        Guid eventId,
        string consumerName,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
