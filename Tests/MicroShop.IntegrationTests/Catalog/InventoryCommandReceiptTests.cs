using InventoryService.Application.Abstractions;
using InventoryService.Infrastructure.Persistence;
using InventoryService.Infrastructure.Persistence.Outbox;
using Dapper;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace MicroShop.IntegrationTests.Inventory;

public sealed class InventoryCommandReceiptTests
{
    [Fact]
    public async Task DuplicateCommitMessage_ChangesStockOnceAndStoresOneReceipt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("inventory_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await postgres.StartAsync(cancellationToken);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:InventoryDb"] = postgres.GetConnectionString()
            })
            .Build();

        var connectionFactory = new NpgsqlConnectionFactory(configuration);
        await new PostgresDatabaseInitializer(configuration).InitializeAsync(cancellationToken);

        const string productId = "product-001";
        var orderId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        using (var connection = connectionFactory.CreateConnection())
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO InventoryItems (ProductId, StockQuantity, ReservedQuantity, UpdatedAtUtc)
                VALUES (@ProductId, 10, 0, CURRENT_TIMESTAMP);
                """, new { ProductId = productId }, cancellationToken: cancellationToken));
        }

        var repository = new DapperInventoryReservationRepository(connectionFactory, new DapperInventoryOutboxRepository(connectionFactory));
        var reservation = await repository.ReserveAsync(
            orderId,
            [new InventoryReservationItem(productId, 2)],
            DateTime.UtcNow.AddMinutes(30),
            cancellationToken);

        await repository.CommitAsync(orderId, messageId, cancellationToken);
        await repository.CommitAsync(orderId, messageId, cancellationToken);

        using var verificationConnection = connectionFactory.CreateConnection();
        var stock = await verificationConnection.QuerySingleAsync<(int StockQuantity, int ReservedQuantity)>(new CommandDefinition("""
            SELECT StockQuantity, ReservedQuantity
            FROM InventoryItems
            WHERE ProductId = @ProductId;
            """, new { ProductId = productId }, cancellationToken: cancellationToken));
        var status = await verificationConnection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT Status FROM InventoryReservations WHERE OrderId = @OrderId;",
            new { OrderId = orderId }, cancellationToken: cancellationToken));
        var receiptCount = await verificationConnection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM InventoryCommandReceipts WHERE EventId = @EventId;",
            new { EventId = messageId }, cancellationToken: cancellationToken));
        var outcomeCount = await verificationConnection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM InventoryOutboxMessages WHERE CausationId = @CausationId;",
            new { CausationId = messageId.ToString("D") }, cancellationToken: cancellationToken));

        Assert.True(reservation.Succeeded);
        Assert.Equal(8, stock.StockQuantity);
        Assert.Equal(0, stock.ReservedQuantity);
        Assert.Equal("Committed", status);
        Assert.Equal(1, receiptCount);
        Assert.Equal(1, outcomeCount);
    }
}
