using CatalogService.Application.Products.CreateProduct;
using CatalogService.Domain.Products;
using CatalogService.Infrastructure.Persistence;
using CatalogService.Infrastructure.Persistence.Outbox;
using Dapper;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace MicroShop.IntegrationTests.Catalog;

public sealed class CatalogProductProvisioningTests
{
    [Fact]
    public async Task CreateProduct_PersistsProductAndInventoryProvisionEventInTheSameDatabase()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("catalog_provisioning_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await postgres.StartAsync(cancellationToken);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CatalogDb"] = postgres.GetConnectionString()
            })
            .Build();

        var connectionFactory = new NpgsqlConnectionFactory(configuration);
        await new PostgresDatabaseInitializer(configuration).InitializeAsync(cancellationToken);

        var handler = new CreateProductHandler(
            new DapperProductRepository(connectionFactory),
            new DapperCatalogUnitOfWork(connectionFactory),
            new DapperCatalogOutboxRepository(connectionFactory));

        var created = await handler.Handle(
            new CreateProductCommand("Provisioned product", 19.99m, "Outbox test", 5),
            cancellationToken);

        using var connection = connectionFactory.CreateConnection();
        var productCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Products WHERE Id = @Id;", new { created.Id }, cancellationToken: cancellationToken));
        var eventCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*)
            FROM CatalogOutboxMessages
            WHERE Type = 'BuildingBlocks.Contracts.Events.Inventory.InventoryItemProvisionRequestedIntegrationEvent'
              AND Content ->> 'productId' = @ProductId;
            """, new { ProductId = created.Id }, cancellationToken: cancellationToken));

        Assert.Equal(1, productCount);
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public async Task InventoryAvailabilitySnapshot_DoesNotAllowAnOlderEventToOverwriteNewerStock()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("catalog_inventory_snapshot_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await postgres.StartAsync(cancellationToken);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CatalogDb"] = postgres.GetConnectionString()
            })
            .Build();

        var connectionFactory = new NpgsqlConnectionFactory(configuration);
        await new PostgresDatabaseInitializer(configuration).InitializeAsync(cancellationToken);

        var repository = new DapperProductRepository(connectionFactory);
        var product = new Product("snapshot-product-001", "Snapshot product", "Catalog snapshot test", 9.99m, 20);
        await repository.CreateAsync(product, cancellationToken);

        var latestSnapshotAtUtc = DateTime.UtcNow;
        var latestApplied = await repository.UpdateInventoryAvailabilitySnapshotAsync(
            product.Id,
            8,
            latestSnapshotAtUtc,
            cancellationToken);
        var staleApplied = await repository.UpdateInventoryAvailabilitySnapshotAsync(
            product.Id,
            20,
            latestSnapshotAtUtc.AddSeconds(-1),
            cancellationToken);
        var stored = await repository.GetByIdAsync(product.Id, cancellationToken);

        Assert.True(latestApplied);
        Assert.False(staleApplied);
        Assert.NotNull(stored);
        Assert.Equal(8, stored.StockQuantity);
    }
}
