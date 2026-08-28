using IdentityService.Application.Addresses;
using IdentityService.Application.Auth;
using IdentityService.Infrastructure.Auth;
using IdentityService.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace MicroShop.IntegrationTests.Identity;

public sealed class CustomerAddressRepositoryTests
{
    [Fact]
    public async Task Create_ReplaysIdempotently_AndMaintainsOneDefaultPerCustomer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("identity_address_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await postgres.StartAsync(cancellationToken);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:IdentityDb"] = postgres.GetConnectionString() })
            .Build();
        var connectionFactory = new NpgsqlConnectionFactory(configuration);
        await new PostgresDatabaseInitializer(configuration).InitializeAsync(cancellationToken);

        var registered = await new RegisterHandler(new DapperUserRepository(connectionFactory), new Pbkdf2PasswordHasher(), NullLogger<RegisterHandler>.Instance)
            .Handle(new RegisterCommand("address-customer", "address-customer@example.test", "CustomerPassword#2026"), cancellationToken);
        var service = new AddressService(new DapperAddressRepository(connectionFactory));
        var firstInput = new AddressInput("Home", "Ada Lovelace", "1 Main Street", null, "Hanoi", "VN", "10000", false);

        var first = await service.CreateAsync(registered.UserId, firstInput, "address-create-1", cancellationToken);
        var replay = await service.CreateAsync(registered.UserId, firstInput, "address-create-1", cancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(registered.UserId, firstInput with { City = "Da Nang" }, "address-create-1", cancellationToken));
        var second = await service.CreateAsync(registered.UserId, firstInput with { Label = "Office", MakeDefault = true }, "address-create-2", cancellationToken);
        var updated = await service.UpdateAsync(registered.UserId, first.Id, firstInput with { Line1 = "2 Main Street", MakeDefault = true }, cancellationToken);

        var addresses = await service.GetAsync(registered.UserId, cancellationToken);
        Assert.Equal(first.Id, replay.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsDefault);
        Assert.Single(addresses, address => address.IsDefault);
        Assert.True(addresses.Single(address => address.Id == first.Id).IsDefault);
        Assert.False(addresses.Single(address => address.Id == second.Id).IsDefault);
        Assert.Null(await service.GetAsync(Guid.NewGuid(), first.Id, cancellationToken));
    }
}
