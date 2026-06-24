using Dapper;
using Microsoft.Extensions.Configuration;
using PaymentService.Domain.Payments;
using PaymentService.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace MicroShop.IntegrationTests.Payment;

public sealed class PaymentWebhookIdempotencyTests
{
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
}
