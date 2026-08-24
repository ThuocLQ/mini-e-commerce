using System.Data;
using Dapper;
using OrderingService.Application.Abstractions;

namespace OrderingService.Infrastructure.Persistence;

public sealed class DapperInboxRepository : IInboxRepository
{
    public async Task<bool> TryRecordAsync(
        Guid eventId,
        string consumerName,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await transaction.Connection!.ExecuteAsync(new CommandDefinition("""
            INSERT INTO InboxMessages (EventId, ConsumerName, ReceivedAtUtc)
            VALUES (@EventId, @ConsumerName, CURRENT_TIMESTAMP)
            ON CONFLICT (ConsumerName, EventId) DO NOTHING;
            """, new { EventId = eventId, ConsumerName = consumerName }, transaction, cancellationToken: cancellationToken));

        return affectedRows == 1;
    }
}
