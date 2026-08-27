using Microsoft.Extensions.Configuration;
using OrderingService.Domain.Orders;
using OrderingService.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace MicroShop.IntegrationTests.Ordering;

public sealed class OrderAddressSnapshotPersistenceTests
{
    [Fact]
    public async Task ShippingAddressSnapshot_IsPersistedIndependentlyOfTheSourceProfile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("ordering_address_snapshot_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await postgres.StartAsync(cancellationToken);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:OrderingDb"] = postgres.GetConnectionString() })
            .Build();
        await new PostgresDatabaseInitializer(configuration).InitializeAsync(cancellationToken);
        var repository = new DapperOrderRepository(new NpgsqlConnectionFactory(configuration));
        var addressId = Guid.NewGuid();
        var order = new Order(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            OrderStatus.PendingPayment,
            "address-snapshot-checkout",
            "USD",
            new string('a', 64),
            checkoutBasketVersion: 1,
            checkoutBasketId: Guid.NewGuid(),
            shippingAddress: new OrderAddressSnapshot(addressId, "Home", "Ada Lovelace", "1 Main Street", "Apt 2", "Hanoi", "VN", "10000"));
        order.AddItem(new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Keyboard", 100m, 1));

        await repository.CreateAsync(order, cancellationToken: cancellationToken);
        var reloaded = await repository.GetByIdAsync(order.Id, cancellationToken);

        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded.ShippingAddress);
        Assert.Equal(addressId, reloaded.ShippingAddress.AddressId);
        Assert.Equal("1 Main Street", reloaded.ShippingAddress.Line1);
        Assert.Equal("Apt 2", reloaded.ShippingAddress.Line2);
    }
}
