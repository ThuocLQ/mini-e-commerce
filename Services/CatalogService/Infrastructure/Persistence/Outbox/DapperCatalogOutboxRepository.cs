using System.Data;
using CatalogService.Application.Abstractions;
using CatalogService.Domain.Outbox;
using Dapper;

namespace CatalogService.Infrastructure.Persistence.Outbox;

public sealed class DapperCatalogOutboxRepository : ICatalogOutboxRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperCatalogOutboxRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task AddAsync(CatalogOutboxMessage message, IDbTransaction transaction, CancellationToken cancellationToken = default) =>
        transaction.Connection!.ExecuteAsync(new CommandDefinition("""
            INSERT INTO CatalogOutboxMessages (Id, OccurredAtUtc, Type, Content, CorrelationId, CausationId, NextAttemptAtUtc)
            VALUES (@Id, @OccurredAtUtc, @Type, CAST(@Content AS jsonb), @CorrelationId, @CausationId, @NextAttemptAtUtc);
            """, message, transaction, cancellationToken: cancellationToken));

    public async Task<IReadOnlyList<CatalogOutboxMessage>> ClaimPendingAsync(
        int batchSize,
        int maxRetryCount,
        Guid lockId,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var messages = await connection.QueryAsync<CatalogOutboxMessage>(new CommandDefinition("""
            WITH candidates AS (
                SELECT Id
                FROM CatalogOutboxMessages
                WHERE ProcessedAtUtc IS NULL
                  AND RetryCount < @MaxRetryCount
                  AND NextAttemptAtUtc <= CURRENT_TIMESTAMP
                  AND (LockedUntilUtc IS NULL OR LockedUntilUtc <= CURRENT_TIMESTAMP)
                ORDER BY OccurredAtUtc
                LIMIT @BatchSize
                FOR UPDATE SKIP LOCKED
            )
            UPDATE CatalogOutboxMessages message
            SET LockId = @LockId,
                LockedUntilUtc = CURRENT_TIMESTAMP + make_interval(secs => @LockDurationSeconds)
            FROM candidates
            WHERE message.Id = candidates.Id
            RETURNING message.Id,
                      message.OccurredAtUtc,
                      message.Type,
                      message.Content::text AS Content,
                      message.CorrelationId,
                      message.CausationId,
                      message.NextAttemptAtUtc,
                      message.ProcessedAtUtc,
                      message.RetryCount,
                      message.LastError,
                      message.LockId,
                      message.LockedUntilUtc;
            """, new
        {
            BatchSize = batchSize,
            MaxRetryCount = maxRetryCount,
            LockId = lockId,
            LockDurationSeconds = checked((int)lockDuration.TotalSeconds)
        }, cancellationToken: cancellationToken));

        return messages.AsList();
    }

    public async Task<bool> MarkAsProcessedAsync(
        Guid id,
        Guid lockId,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE CatalogOutboxMessages
            SET ProcessedAtUtc = CURRENT_TIMESTAMP,
                LastError = NULL,
                LockId = NULL,
                LockedUntilUtc = NULL
            WHERE Id = @Id
              AND LockId = @LockId
              AND LockedUntilUtc > CURRENT_TIMESTAMP;
            """, new { Id = id, LockId = lockId }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<bool> MarkAsFailedAsync(
        Guid id,
        Guid lockId,
        string error,
        DateTime nextAttemptAtUtc,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE CatalogOutboxMessages
            SET RetryCount = RetryCount + 1,
                LastError = @LastError,
                NextAttemptAtUtc = @NextAttemptAtUtc,
                LockId = NULL,
                LockedUntilUtc = NULL
            WHERE Id = @Id
              AND LockId = @LockId
              AND LockedUntilUtc > CURRENT_TIMESTAMP;
            """, new
        {
            Id = id,
            LockId = lockId,
            LastError = Truncate(error, 4000),
            NextAttemptAtUtc = nextAttemptAtUtc
        }, cancellationToken: cancellationToken)) == 1;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
