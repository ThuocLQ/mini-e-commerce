using Dapper;
using PaymentService.Application.Abstractions;
using PaymentService.Domain.Outbox;

namespace PaymentService.Infrastructure.Persistence;

public sealed class DapperPaymentOutboxRepository : IPaymentOutboxRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperPaymentOutboxRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task AddAsync(
        PaymentOutboxMessage message,
        System.Data.IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = ToParameters(message);

        if (transaction is not null)
        {
            await InsertAsync(transaction.Connection!, transaction, parameters, cancellationToken);
            return;
        }

        using var connection = _connectionFactory.CreateConnection();
        await InsertAsync(connection, null, parameters, cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentOutboxMessage>> ClaimPendingAsync(
        int batchSize,
        int maxRetryCount,
        Guid lockId,
        DateTime nowUtc,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var rows = await connection.QueryAsync<PaymentOutboxMessageRow>(new CommandDefinition("""
            WITH claim AS (
                SELECT Id
                FROM PaymentOutboxMessages
                WHERE Status IN ('Pending', 'Failed')
                  AND RetryCount < @MaxRetryCount
                  AND NextAttemptAtUtc <= @NowUtc
                ORDER BY OccurredAtUtc
                LIMIT @BatchSize
                FOR UPDATE SKIP LOCKED
            )
            UPDATE PaymentOutboxMessages message
            SET Status = 'Processing',
                LockedBy = @LockId,
                LockedUntilUtc = @LockedUntilUtc
            FROM claim
            WHERE message.Id = claim.Id
            RETURNING message.Id, message.OccurredAtUtc, message.Type, message.Content, message.Status,
                      message.CorrelationId, message.CausationId, message.RetryCount, message.Error,
                      message.NextAttemptAtUtc, message.ProcessedAtUtc;
            """, new
        {
            BatchSize = batchSize,
            MaxRetryCount = maxRetryCount,
            NowUtc = nowUtc,
            LockId = lockId,
            LockedUntilUtc = nowUtc.Add(lockDuration)
        }, cancellationToken: cancellationToken));

        return rows.Select(Map).ToList();
    }

    public async Task MarkAsProcessedAsync(
        Guid messageId,
        Guid lockId,
        DateTime processedAtUtc,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE PaymentOutboxMessages
            SET Status = 'Processed',
                ProcessedAtUtc = @ProcessedAtUtc,
                Error = NULL,
                LockedBy = NULL,
                LockedUntilUtc = NULL
            WHERE Id = @MessageId
              AND LockedBy = @LockId;
            """, new { MessageId = messageId, LockId = lockId, ProcessedAtUtc = processedAtUtc }, cancellationToken: cancellationToken));
    }

    public async Task MarkAsFailedAsync(
        Guid messageId,
        Guid lockId,
        string error,
        DateTime nextAttemptAtUtc,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE PaymentOutboxMessages
            SET Status = 'Failed',
                RetryCount = RetryCount + 1,
                Error = @Error,
                NextAttemptAtUtc = @NextAttemptAtUtc,
                LockedBy = NULL,
                LockedUntilUtc = NULL
            WHERE Id = @MessageId
              AND LockedBy = @LockId;
            """, new
        {
            MessageId = messageId,
            LockId = lockId,
            Error = Truncate(error, 4000),
            NextAttemptAtUtc = nextAttemptAtUtc
        }, cancellationToken: cancellationToken));
    }

    private static Task InsertAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction? transaction,
        object parameters,
        CancellationToken cancellationToken)
    {
        return connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO PaymentOutboxMessages (
                Id, OccurredAtUtc, Type, Content, CorrelationId, CausationId, Status, RetryCount, Error, NextAttemptAtUtc, ProcessedAtUtc)
            VALUES (
                @Id, @OccurredAtUtc, @Type, @Content, @CorrelationId, @CausationId, @Status, @RetryCount, @Error, @NextAttemptAtUtc, @ProcessedAtUtc)
            ON CONFLICT (Id) DO NOTHING;
            """, parameters, transaction, cancellationToken: cancellationToken));
    }

    private static object ToParameters(PaymentOutboxMessage message)
    {
        return new
        {
            message.Id,
            message.OccurredAtUtc,
            message.Type,
            message.Content,
            message.CorrelationId,
            message.CausationId,
            message.Status,
            message.RetryCount,
            message.Error,
            message.NextAttemptAtUtc,
            message.ProcessedAtUtc
        };
    }

    private static PaymentOutboxMessage Map(PaymentOutboxMessageRow row)
    {
        return new PaymentOutboxMessage
        {
            Id = row.Id,
            OccurredAtUtc = row.OccurredAtUtc,
            Type = row.Type,
            Content = row.Content,
            CorrelationId = row.CorrelationId,
            CausationId = row.CausationId,
            Status = row.Status,
            RetryCount = row.RetryCount,
            Error = row.Error,
            NextAttemptAtUtc = row.NextAttemptAtUtc,
            ProcessedAtUtc = row.ProcessedAtUtc
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record PaymentOutboxMessageRow(
        Guid Id,
        DateTime OccurredAtUtc,
        string Type,
        string Content,
        string? CorrelationId,
        string? CausationId,
        string Status,
        int RetryCount,
        string? Error,
        DateTime NextAttemptAtUtc,
        DateTime? ProcessedAtUtc);
}
