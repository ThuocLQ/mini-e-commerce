using System.Data;
using Dapper;
using OrderingService.Application.Abstractions;
using OrderingService.Domain.Outbox;
using OrderingService.Infrastructure.Persistence;

namespace OrderingService.Infrastructure.Outbox;

public sealed class DapperOutboxRepository : IOutboxRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperOutboxRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task AddAsync(
        OutboxMessage message,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO OutboxMessages (
                Id,
                OccurredAtUtc,
                Type,
                Content,
                CorrelationId,
                CausationId,
                NextAttemptAtUtc,
                ProcessedAtUtc,
                RetryCount,
                LastError,
                LockId,
                LockedUntilUtc
            )
            VALUES (
                @Id,
                @OccurredAtUtc,
                @Type,
                CAST(@Content AS jsonb),
                @CorrelationId,
                @CausationId,
                @NextAttemptAtUtc,
                @ProcessedAtUtc,
                @RetryCount,
                @LastError,
                @LockId,
                @LockedUntilUtc
            );
            """;

        if (transaction is not null)
        {
            await transaction.Connection!.ExecuteAsync(new CommandDefinition(
                sql,
                message,
                transaction,
                cancellationToken: cancellationToken));
            return;
        }

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            message,
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
        int batchSize,
        int maxRetryCount,
        Guid lockId,
        DateTime nowUtc,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH CandidateMessages AS (
                SELECT Id
                FROM OutboxMessages
                WHERE ProcessedAtUtc IS NULL
                  AND RetryCount < @MaxRetryCount
                  AND NextAttemptAtUtc <= @NowUtc
                  AND (LockedUntilUtc IS NULL OR LockedUntilUtc <= @NowUtc)
                ORDER BY OccurredAtUtc
                LIMIT @BatchSize
                FOR UPDATE SKIP LOCKED
            )
            UPDATE OutboxMessages outbox
            SET
                LockId = @LockId,
                LockedUntilUtc = @LockedUntilUtc
            FROM CandidateMessages candidate
            WHERE outbox.Id = candidate.Id
            RETURNING
                outbox.Id,
                outbox.OccurredAtUtc,
                outbox.Type,
                outbox.Content::text AS Content,
                outbox.CorrelationId,
                outbox.CausationId,
                outbox.NextAttemptAtUtc,
                outbox.ProcessedAtUtc,
                outbox.RetryCount,
                outbox.LastError,
                outbox.LockId,
                outbox.LockedUntilUtc;
            """;

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var messages = await connection.QueryAsync<OutboxMessage>(new CommandDefinition(
            sql,
            new
            {
                BatchSize = batchSize,
                MaxRetryCount = maxRetryCount,
                LockId = lockId,
                NowUtc = nowUtc,
                LockedUntilUtc = nowUtc.Add(lockDuration)
            },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();

        return messages.AsList();
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetLatestAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                Id,
                OccurredAtUtc,
                Type,
                Content::text AS Content,
                CorrelationId,
                CausationId,
                NextAttemptAtUtc,
                ProcessedAtUtc,
                RetryCount,
                LastError,
                LockId,
                LockedUntilUtc
            FROM OutboxMessages
            ORDER BY OccurredAtUtc DESC
            LIMIT @Limit;
            """;

        using var connection = _connectionFactory.CreateConnection();

        var messages = await connection.QueryAsync<OutboxMessage>(new CommandDefinition(
            sql,
            new { Limit = limit },
            cancellationToken: cancellationToken));

        return messages.AsList();
    }

    public async Task MarkAsProcessedAsync(
        Guid id,
        Guid lockId,
        DateTime processedAtUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE OutboxMessages
            SET
                ProcessedAtUtc = @ProcessedAtUtc,
                LastError = NULL,
                LockId = NULL,
                LockedUntilUtc = NULL
            WHERE Id = @Id
              AND LockId = @LockId;
            """;

        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                Id = id,
                LockId = lockId,
                ProcessedAtUtc = processedAtUtc
            },
            cancellationToken: cancellationToken));
    }

    public async Task MarkAsFailedAsync(
        Guid id,
        Guid lockId,
        string error,
        DateTime nextAttemptAtUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE OutboxMessages
            SET
                RetryCount = RetryCount + 1,
                LastError = @LastError,
                NextAttemptAtUtc = @NextAttemptAtUtc,
                LockId = NULL,
                LockedUntilUtc = NULL
            WHERE Id = @Id
              AND LockId = @LockId;
            """;

        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                Id = id,
                LockId = lockId,
                LastError = Truncate(error, 4000),
                NextAttemptAtUtc = nextAttemptAtUtc
            },
            cancellationToken: cancellationToken));
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
