using CatalogService.Application.Products.CreateProduct;
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
}
