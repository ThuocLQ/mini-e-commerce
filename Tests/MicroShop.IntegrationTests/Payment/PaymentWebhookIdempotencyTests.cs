using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PaymentService.Domain.Payments;
using PaymentService.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace MicroShop.IntegrationTests.Payment;

public sealed class PaymentWebhookIdempotencyTests
{
    [Theory]
    [InlineData(PaymentStatus.Authorized, "PaymentAuthorizedIntegrationEvent", "AuthorizedAtUtc")]
    [InlineData(PaymentStatus.Captured, "PaymentCapturedIntegrationEvent", "CapturedAtUtc")]
    public async Task LifecycleWebhook_PersistsExpectedStateTimestampAndOutboxEvent(
        PaymentStatus webhookStatus,
        string expectedEventType,
        string timestampColumn)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgres = CreatePostgres();
        await postgres.StartAsync(cancellationToken);

        var connectionFactory = await InitializeDatabaseAsync(postgres, cancellationToken);
        var payment = new PaymentService.Domain.Payments.Payment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            125_000m,
            "VND",
            PaymentStatus.PendingAuthorization,
            DateTime.UtcNow.AddMinutes(-1));
        if (webhookStatus == PaymentStatus.Captured)
        {
            payment.MarkAuthorized("provider-transaction-001", DateTime.UtcNow.AddSeconds(-2));
            payment.RequestCapture(DateTime.UtcNow.AddSeconds(-1));
        }
        await new DapperPaymentRepository(connectionFactory).CreateAsync(payment, cancellationToken);

        var receivedAtUtc = DateTime.UtcNow;
        var result = await new DapperPaymentWebhookRepository(connectionFactory).ApplyAsync(
            $"evt-{webhookStatus.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}",
            payment.Id,
            "provider-transaction-001",
            webhookStatus,
            null,
            "payload-hash",
            "Verified",
            receivedAtUtc,
            cancellationToken);

        using var connection = connectionFactory.CreateConnection();
        var persisted = await connection.QuerySingleAsync<LifecyclePaymentState>(new CommandDefinition($"""
            SELECT Status, AuthorizedAtUtc, CapturedAtUtc
            FROM Payments
            WHERE Id = @PaymentId;
            """, new { PaymentId = payment.Id }, cancellationToken: cancellationToken));
        var outbox = await connection.QuerySingleAsync<OutboxState>(new CommandDefinition("""
            SELECT Type, Content, CorrelationId, CausationId
            FROM PaymentOutboxMessages;
            """, cancellationToken: cancellationToken));

        Assert.False(result.IsDuplicate);
        Assert.Equal(webhookStatus.ToString(), persisted.Status);
        Assert.NotNull(timestampColumn == "AuthorizedAtUtc" ? persisted.AuthorizedAtUtc : persisted.CapturedAtUtc);
        Assert.EndsWith(expectedEventType, outbox.Type, StringComparison.Ordinal);
        Assert.Contains($"\"paymentId\":\"{payment.Id}\"", outbox.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(payment.OrderId.ToString("N"), outbox.CorrelationId);
    }

    [Theory]
    [InlineData(PaymentStatus.Voided, "PaymentVoidedIntegrationEvent", "VoidedAtUtc")]
    [InlineData(PaymentStatus.Refunded, "PaymentRefundedIntegrationEvent", "RefundedAtUtc")]
    public async Task TerminalLifecycleWebhook_PersistsExpectedOutcome(
        PaymentStatus webhookStatus,
        string expectedEventType,
        string timestampColumn)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgres = CreatePostgres();
        await postgres.StartAsync(cancellationToken);

        var connectionFactory = await InitializeDatabaseAsync(postgres, cancellationToken);
        var payment = CreatePaymentReadyForTerminalWebhook(webhookStatus);
        await new DapperPaymentRepository(connectionFactory).CreateAsync(payment, cancellationToken);

        await new DapperPaymentWebhookRepository(connectionFactory).ApplyAsync(
            $"evt-{webhookStatus.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}",
            payment.Id,
            "provider-transaction-001",
            webhookStatus,
            null,
            "payload-hash",
            "Verified",
            DateTime.UtcNow,
            cancellationToken);

        using var connection = connectionFactory.CreateConnection();
        var persisted = await connection.QuerySingleAsync<TerminalLifecyclePaymentState>(new CommandDefinition("""
            SELECT Status, VoidedAtUtc, RefundedAtUtc
            FROM Payments
            WHERE Id = @PaymentId;
            """, new { PaymentId = payment.Id }, cancellationToken: cancellationToken));
        var outboxType = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT Type FROM PaymentOutboxMessages;",
            cancellationToken: cancellationToken));

        Assert.Equal(webhookStatus.ToString(), persisted.Status);
        Assert.NotNull(timestampColumn == "VoidedAtUtc" ? persisted.VoidedAtUtc : persisted.RefundedAtUtc);
        Assert.EndsWith(expectedEventType, outboxType, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedWebhook_AfterAuthorization_IsRejectedAndRollsBackWebhookLog()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgres = CreatePostgres();
        await postgres.StartAsync(cancellationToken);

        var connectionFactory = await InitializeDatabaseAsync(postgres, cancellationToken);
        var payment = new PaymentService.Domain.Payments.Payment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 125_000m, "VND", PaymentStatus.PendingAuthorization, DateTime.UtcNow);
        payment.MarkAuthorized("provider-transaction-001", DateTime.UtcNow);
        await new DapperPaymentRepository(connectionFactory).CreateAsync(payment, cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new DapperPaymentWebhookRepository(connectionFactory).ApplyAsync(
            "evt-failed-after-authorization",
            payment.Id,
            "provider-transaction-001",
            PaymentStatus.Failed,
            "Provider declined after authorization.",
            "payload-hash",
            "Verified",
            DateTime.UtcNow,
            cancellationToken));

        using var connection = connectionFactory.CreateConnection();
        var webhookCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM WebhookLogs;", cancellationToken: cancellationToken));
        var persistedStatus = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT Status FROM Payments WHERE Id = @PaymentId;", new { PaymentId = payment.Id }, cancellationToken: cancellationToken));

        Assert.Equal(0, webhookCount);
        Assert.Equal(PaymentStatus.Authorized.ToString(), persistedStatus);
    }

    [Fact]
    public async Task DuplicateProviderEvent_CreatesOneWebhookLogAndOneOutboxMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("payment_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await postgres.StartAsync(cancellationToken);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PaymentDb"] = postgres.GetConnectionString()
            })
            .Build();

        var connectionFactory = new NpgsqlConnectionFactory(configuration);
        var initializer = new PostgresDatabaseInitializer(configuration);
        await initializer.InitializeAsync(cancellationToken);

        var payment = new PaymentService.Domain.Payments.Payment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            125_000m,
            "VND",
            PaymentStatus.Pending,
            DateTime.UtcNow);

        payment.MarkAuthorized("provider-transaction-001", DateTime.UtcNow.AddSeconds(-2));
        payment.RequestCapture(DateTime.UtcNow.AddSeconds(-1));

        await new DapperPaymentRepository(connectionFactory).CreateAsync(
            payment,
            cancellationToken);

        var repository = new DapperPaymentWebhookRepository(connectionFactory);
        const string providerEventId = "evt-idempotency-001";

        var first = await repository.ApplyAsync(
            providerEventId,
            payment.Id,
            "provider-transaction-001",
            PaymentStatus.Succeeded,
            null,
            "payload-hash",
            "Verified",
            DateTime.UtcNow,
            cancellationToken);

        var duplicate = await repository.ApplyAsync(
            providerEventId,
            payment.Id,
            "provider-transaction-001",
            PaymentStatus.Succeeded,
            null,
            "payload-hash",
            "Verified",
            DateTime.UtcNow,
            cancellationToken);

        using var connection = connectionFactory.CreateConnection();
        var webhookCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM WebhookLogs WHERE ProviderEventId = @ProviderEventId;",
            new { ProviderEventId = providerEventId },
            cancellationToken: cancellationToken));
        var outboxCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM PaymentOutboxMessages;",
            cancellationToken: cancellationToken));

        Assert.False(first.IsDuplicate);
        Assert.True(duplicate.IsDuplicate);
        Assert.Equal(1, webhookCount);
        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public async Task DuplicateProviderEvent_WithConflictingPayload_IsAuditedAndRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("payment_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await postgres.StartAsync(cancellationToken);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PaymentDb"] = postgres.GetConnectionString()
            })
            .Build();

        var connectionFactory = new NpgsqlConnectionFactory(configuration);
        await new PostgresDatabaseInitializer(configuration).InitializeAsync(cancellationToken);

        var payment = new PaymentService.Domain.Payments.Payment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 125_000m, "VND", PaymentStatus.Pending, DateTime.UtcNow);
        payment.MarkAuthorized("provider-transaction-001", DateTime.UtcNow.AddSeconds(-2));
        payment.RequestCapture(DateTime.UtcNow.AddSeconds(-1));
        await new DapperPaymentRepository(connectionFactory).CreateAsync(payment, cancellationToken);

        var repository = new DapperPaymentWebhookRepository(connectionFactory);
        const string providerEventId = "evt-conflict-001";

        await repository.ApplyAsync(providerEventId, payment.Id, "provider-transaction-001", PaymentStatus.Succeeded,
            null, "original-payload-hash", "Verified", DateTime.UtcNow, cancellationToken);

        await Assert.ThrowsAsync<PaymentService.Application.Payments.Webhooks.PaymentWebhookIntegrityException>(() =>
            repository.ApplyAsync(providerEventId, payment.Id, "provider-transaction-001", PaymentStatus.Succeeded,
                null, "different-payload-hash", "Verified", DateTime.UtcNow, cancellationToken));

        using var connection = connectionFactory.CreateConnection();
        var conflicts = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM WebhookEventConflicts WHERE ProviderEventId = @ProviderEventId;",
            new { ProviderEventId = providerEventId }, cancellationToken: cancellationToken));
        var outboxCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM PaymentOutboxMessages;", cancellationToken: cancellationToken));

        Assert.Equal(1, conflicts);
        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public async Task OutboxWriteFailure_RollsBackPaymentStateAndWebhookLog()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("payment_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await postgres.StartAsync(cancellationToken);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PaymentDb"] = postgres.GetConnectionString()
            })
            .Build();

        var connectionFactory = new NpgsqlConnectionFactory(configuration);
        await new PostgresDatabaseInitializer(configuration).InitializeAsync(cancellationToken);

        var payment = new PaymentService.Domain.Payments.Payment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 125_000m, "VND", PaymentStatus.Pending, DateTime.UtcNow);
        payment.MarkAuthorized("provider-transaction-001", DateTime.UtcNow.AddSeconds(-2));
        payment.RequestCapture(DateTime.UtcNow.AddSeconds(-1));
        await new DapperPaymentRepository(connectionFactory).CreateAsync(payment, cancellationToken);

        using (var connection = connectionFactory.CreateConnection())
        {
            await connection.ExecuteAsync("DROP TABLE PaymentOutboxMessages;");
        }

        var repository = new DapperPaymentWebhookRepository(connectionFactory);
        await Assert.ThrowsAsync<PostgresException>(() => repository.ApplyAsync(
            "evt-outbox-write-failure-001", payment.Id, "provider-transaction-001", PaymentStatus.Succeeded,
            null, "payload-hash", "Verified", DateTime.UtcNow, cancellationToken));

        using var verificationConnection = connectionFactory.CreateConnection();
        var persistedStatus = await verificationConnection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT Status FROM Payments WHERE Id = @PaymentId;",
            new { PaymentId = payment.Id },
            cancellationToken: cancellationToken));
        var webhookCount = await verificationConnection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM WebhookLogs;",
            cancellationToken: cancellationToken));

        Assert.Equal(PaymentStatus.CapturePending.ToString(), persistedStatus);
        Assert.Equal(0, webhookCount);
    }

    [Fact]
    public async Task CapturedWebhook_BeforeCaptureRequest_IsRejectedAndRollsBackWebhookLog()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgres = CreatePostgres();
        await postgres.StartAsync(cancellationToken);

        var connectionFactory = await InitializeDatabaseAsync(postgres, cancellationToken);
        var payment = new PaymentService.Domain.Payments.Payment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 125_000m, "VND", PaymentStatus.PendingAuthorization, DateTime.UtcNow);
        payment.MarkAuthorized("provider-transaction-001", DateTime.UtcNow);
        await new DapperPaymentRepository(connectionFactory).CreateAsync(payment, cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new DapperPaymentWebhookRepository(connectionFactory).ApplyAsync(
            "evt-captured-without-request",
            payment.Id,
            "provider-transaction-001",
            PaymentStatus.Captured,
            null,
            "payload-hash",
            "Verified",
            DateTime.UtcNow,
            cancellationToken));

        using var connection = connectionFactory.CreateConnection();
        var webhookCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM WebhookLogs;", cancellationToken: cancellationToken));
        var persistedStatus = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT Status FROM Payments WHERE Id = @PaymentId;", new { PaymentId = payment.Id }, cancellationToken: cancellationToken));

        Assert.Equal(0, webhookCount);
        Assert.Equal(PaymentStatus.Authorized.ToString(), persistedStatus);
    }

    [Fact]
    public async Task DuplicateCaptureOutcome_WithDifferentProviderEventId_DoesNotPublishAnotherOutboxEvent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgres = CreatePostgres();
        await postgres.StartAsync(cancellationToken);

        var connectionFactory = await InitializeDatabaseAsync(postgres, cancellationToken);
        var payment = new PaymentService.Domain.Payments.Payment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 125_000m, "VND", PaymentStatus.PendingAuthorization, DateTime.UtcNow);
        payment.MarkAuthorized("provider-transaction-001", DateTime.UtcNow.AddSeconds(-2));
        payment.RequestCapture(DateTime.UtcNow.AddSeconds(-1));
        await new DapperPaymentRepository(connectionFactory).CreateAsync(payment, cancellationToken);

        var repository = new DapperPaymentWebhookRepository(connectionFactory);
        await repository.ApplyAsync(
            "evt-captured-first", payment.Id, "provider-transaction-001", PaymentStatus.Captured,
            null, "payload-hash-first", "Verified", DateTime.UtcNow, cancellationToken);
        await repository.ApplyAsync(
            "evt-captured-retry", payment.Id, "provider-transaction-001", PaymentStatus.Captured,
            null, "payload-hash-retry", "Verified", DateTime.UtcNow, cancellationToken);

        using var connection = connectionFactory.CreateConnection();
        var webhookCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM WebhookLogs;", cancellationToken: cancellationToken));
        var outboxCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM PaymentOutboxMessages;", cancellationToken: cancellationToken));

        Assert.Equal(2, webhookCount);
        Assert.Equal(1, outboxCount);
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

    private static PaymentService.Domain.Payments.Payment CreatePaymentReadyForTerminalWebhook(PaymentStatus webhookStatus)
    {
        var payment = new PaymentService.Domain.Payments.Payment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 125_000m, "VND", PaymentStatus.PendingAuthorization, DateTime.UtcNow.AddMinutes(-1));

        payment.MarkAuthorized("provider-transaction-001", DateTime.UtcNow.AddSeconds(-30));

        if (webhookStatus == PaymentStatus.Voided)
        {
            payment.RequestVoid(DateTime.UtcNow.AddSeconds(-15));
        }
        else
        {
            payment.RequestCapture(DateTime.UtcNow.AddSeconds(-15));
            payment.MarkCaptured("provider-transaction-001", DateTime.UtcNow.AddSeconds(-10));
            payment.RequestRefund(DateTime.UtcNow.AddSeconds(-5));
        }

        return payment;
    }

    private sealed record LifecyclePaymentState(
        string Status,
        DateTime? AuthorizedAtUtc,
        DateTime? CapturedAtUtc);

    private sealed record TerminalLifecyclePaymentState(
        string Status,
        DateTime? VoidedAtUtc,
        DateTime? RefundedAtUtc);

    private sealed record OutboxState(
        string Type,
        string Content,
        string? CorrelationId,
        string? CausationId);
}
