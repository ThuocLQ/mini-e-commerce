using Dapper;
using Microsoft.Extensions.Configuration;
using PaymentService.Domain.Outbox;
using PaymentService.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace MicroShop.IntegrationTests.Payment;

public sealed class PaymentOutboxRecoveryTests
{
    [Fact]
    public async Task ExpiredProcessingLease_IsReclaimedAndCanBeClaimedAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgres = CreatePostgres();
        await postgres.StartAsync(cancellationToken);

        var connectionFactory = await InitializeDatabaseAsync(postgres, cancellationToken);
        var repository = new DapperPaymentOutboxRepository(connectionFactory);
        var messageId = Guid.NewGuid();
        var nowUtc = DateTime.UtcNow;

        await repository.AddAsync(CreateMessage(messageId, nowUtc.AddMinutes(-5)), cancellationToken: cancellationToken);

        using (var connection = connectionFactory.CreateConnection())
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE PaymentOutboxMessages
                SET Status = 'Processing',
                    LockedBy = @LockedBy,
                    LockedUntilUtc = @LockedUntilUtc
                WHERE Id = @MessageId;
                """, new
            {
                MessageId = messageId,
                LockedBy = Guid.NewGuid(),
                LockedUntilUtc = nowUtc.AddMinutes(-1)
            }, cancellationToken: cancellationToken));
        }

        var reclaimedCount = await repository.ReclaimExpiredLocksAsync(cancellationToken);
        var reclaimed = await repository.ClaimPendingAsync(10, 10, Guid.NewGuid(), TimeSpan.FromMinutes(1), cancellationToken);

        using var verificationConnection = connectionFactory.CreateConnection();
        var persisted = await verificationConnection.QuerySingleAsync<OutboxState>(new CommandDefinition("""
            SELECT Status, RetryCount, LockedBy, LockedUntilUtc
            FROM PaymentOutboxMessages
            WHERE Id = @MessageId;
            """, new { MessageId = messageId }, cancellationToken: cancellationToken));

        Assert.Equal(1, reclaimedCount);
        Assert.Contains(reclaimed, message => message.Id == messageId);
        Assert.Equal("Processing", persisted.Status);
        Assert.Equal(1, persisted.RetryCount);
        Assert.NotNull(persisted.LockedBy);
        Assert.NotNull(persisted.LockedUntilUtc);
    }

    [Fact]
    public async Task ExpiredProcessingLease_ExhaustsTheSameRetryBudgetAsDispatchFailures()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgres = CreatePostgres();
        await postgres.StartAsync(cancellationToken);

        var connectionFactory = await InitializeDatabaseAsync(postgres, cancellationToken);
        var repository = new DapperPaymentOutboxRepository(connectionFactory);
        var messageId = Guid.NewGuid();
        var nowUtc = DateTime.UtcNow;

        await repository.AddAsync(CreateMessage(messageId, nowUtc.AddMinutes(-5)), cancellationToken: cancellationToken);

        using (var connection = connectionFactory.CreateConnection())
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE PaymentOutboxMessages
                SET Status = 'Processing',
                    RetryCount = 9,
                    LockedBy = @LockedBy,
                    LockedUntilUtc = @LockedUntilUtc
                WHERE Id = @MessageId;
                """, new
            {
                MessageId = messageId,
                LockedBy = Guid.NewGuid(),
                LockedUntilUtc = nowUtc.AddMinutes(-1)
            }, cancellationToken: cancellationToken));
        }

        await repository.ReclaimExpiredLocksAsync(cancellationToken);
        var claimed = await repository.ClaimPendingAsync(10, 10, Guid.NewGuid(), TimeSpan.FromMinutes(1), cancellationToken);

        using var verificationConnection = connectionFactory.CreateConnection();
        var persisted = await verificationConnection.QuerySingleAsync<OutboxState>(new CommandDefinition("""
            SELECT Status, RetryCount, LockedBy, LockedUntilUtc
            FROM PaymentOutboxMessages
            WHERE Id = @MessageId;
            """, new { MessageId = messageId }, cancellationToken: cancellationToken));

        Assert.Empty(claimed);
        Assert.Equal("Failed", persisted.Status);
        Assert.Equal(10, persisted.RetryCount);
        Assert.Null(persisted.LockedBy);
        Assert.Null(persisted.LockedUntilUtc);
    }

    [Fact]
    public async Task PreviousLeaseOwner_CannotAcknowledgeMessageAfterItIsReclaimed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgres = CreatePostgres();
        await postgres.StartAsync(cancellationToken);

        var connectionFactory = await InitializeDatabaseAsync(postgres, cancellationToken);
        var repository = new DapperPaymentOutboxRepository(connectionFactory);
        var messageId = Guid.NewGuid();
        var originalLockId = Guid.NewGuid();

        await repository.AddAsync(CreateMessage(messageId, DateTime.UtcNow.AddMinutes(-5)), cancellationToken: cancellationToken);

        using (var connection = connectionFactory.CreateConnection())
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE PaymentOutboxMessages
                SET Status = 'Processing',
                    LockedBy = @LockedBy,
                    LockedUntilUtc = CURRENT_TIMESTAMP - INTERVAL '1 minute'
                WHERE Id = @MessageId;
                """, new { MessageId = messageId, LockedBy = originalLockId }, cancellationToken: cancellationToken));
        }

        await repository.ReclaimExpiredLocksAsync(cancellationToken);
        var newLockId = Guid.NewGuid();
        var reclaimed = await repository.ClaimPendingAsync(10, 10, newLockId, TimeSpan.FromMinutes(1), cancellationToken);
        var markedByOriginalOwner = await repository.MarkAsProcessedAsync(messageId, originalLockId, cancellationToken);

        using var verificationConnection = connectionFactory.CreateConnection();
        var persisted = await verificationConnection.QuerySingleAsync<OutboxState>(new CommandDefinition("""
            SELECT Status, RetryCount, LockedBy, LockedUntilUtc
            FROM PaymentOutboxMessages
            WHERE Id = @MessageId;
            """, new { MessageId = messageId }, cancellationToken: cancellationToken));

        Assert.Contains(reclaimed, message => message.Id == messageId);
        Assert.False(markedByOriginalOwner);
        Assert.Equal("Processing", persisted.Status);
        Assert.Equal(newLockId, persisted.LockedBy);
    }

    private static PostgreSqlContainer CreatePostgres() =>
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("payment_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private static async Task<NpgsqlConnectionFactory> InitializeDatabaseAsync(
        PostgreSqlContainer postgres,
        CancellationToken cancellationToken)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PaymentDb"] = postgres.GetConnectionString()
            })
            .Build();

        var connectionFactory = new NpgsqlConnectionFactory(configuration);
        await new PostgresDatabaseInitializer(configuration).InitializeAsync(cancellationToken);
        return connectionFactory;
    }

    private static PaymentOutboxMessage CreateMessage(Guid id, DateTime occurredAtUtc) => new()
    {
        Id = id,
        OccurredAtUtc = occurredAtUtc,
        Type = "PaymentSucceededIntegrationEvent",
        Content = "{}",
        NextAttemptAtUtc = occurredAtUtc
    };

    private sealed record OutboxState(string Status, int RetryCount, Guid? LockedBy, DateTime? LockedUntilUtc);
}
