using Dapper;
using IdentityService.Application.Auth;
using IdentityService.Infrastructure.Auth;
using IdentityService.Infrastructure.Bootstrap;
using IdentityService.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace MicroShop.IntegrationTests.Identity;

public sealed class AdminBootstrapperTests
{
    [Fact]
    public async Task Bootstrap_CreatesConfiguredAdministratorOnlyOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("identity_bootstrap_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await postgres.StartAsync(cancellationToken);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDb"] = postgres.GetConnectionString()
            })
            .Build();

        var connectionFactory = new NpgsqlConnectionFactory(configuration);
        await new PostgresDatabaseInitializer(configuration).InitializeAsync(cancellationToken);

        var hasher = new Pbkdf2PasswordHasher();
        var bootstrapper = new AdminBootstrapper(
            Options.Create(new BootstrapAdminOptions
            {
                Enabled = true,
                UserName = "bootstrap-admin",
                Password = "BootstrapAdmin#2026"
            }),
            connectionFactory,
            hasher,
            NullLogger<AdminBootstrapper>.Instance);

        await bootstrapper.BootstrapAsync(cancellationToken);
        await bootstrapper.BootstrapAsync(cancellationToken);

        using var connection = connectionFactory.CreateConnection();
        var user = await connection.QuerySingleAsync<UserRow>(new CommandDefinition("""
            SELECT UserName, PasswordHash, Role, IsActive
            FROM Users
            WHERE NormalizedUserName = 'BOOTSTRAP-ADMIN';
            """, cancellationToken: cancellationToken));
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Users WHERE NormalizedUserName = 'BOOTSTRAP-ADMIN';",
            cancellationToken: cancellationToken));

        Assert.Equal(1, count);
        Assert.Equal("bootstrap-admin", user.UserName);
        Assert.Equal("Admin", user.Role);
        Assert.True(user.IsActive);
        Assert.True(hasher.Verify("BootstrapAdmin#2026", user.PasswordHash));
    }

    [Fact]
    public void PasswordHasher_MalformedStoredHash_ReturnsFalse()
    {
        var hasher = new Pbkdf2PasswordHasher();

        Assert.False(hasher.Verify("irrelevant", "PBKDF2-SHA256.100000.not-base64.not-base64"));
        Assert.False(hasher.Verify("irrelevant", "PBKDF2-SHA256.9999999999.AAAA.AAAA"));
    }

    [Fact]
    public async Task RegisterCustomer_CreatesOnlyCustomerRoleAndRejectsDuplicateUsername()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("identity_register_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await postgres.StartAsync(cancellationToken);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDb"] = postgres.GetConnectionString()
            })
            .Build();

        var connectionFactory = new NpgsqlConnectionFactory(configuration);
        await new PostgresDatabaseInitializer(configuration).InitializeAsync(cancellationToken);

        var hasher = new Pbkdf2PasswordHasher();
        var handler = new RegisterHandler(
            new DapperUserRepository(connectionFactory),
            hasher,
            NullLogger<RegisterHandler>.Instance);

        var registered = await handler.Handle(
            new RegisterCommand("customer-one", "CustomerPassword#2026"),
            cancellationToken);

        await Assert.ThrowsAsync<UserNameAlreadyExistsException>(() => handler.Handle(
            new RegisterCommand("CUSTOMER-ONE", "DifferentPassword#2026"),
            cancellationToken));

        using var connection = connectionFactory.CreateConnection();
        var user = await connection.QuerySingleAsync<UserRow>(new CommandDefinition("""
            SELECT UserName, PasswordHash, Role, IsActive
            FROM Users
            WHERE Id = @Id;
            """, new { Id = registered.UserId }, cancellationToken: cancellationToken));

        Assert.Equal("customer-one", user.UserName);
        Assert.Equal("Customer", user.Role);
        Assert.True(user.IsActive);
        Assert.True(hasher.Verify("CustomerPassword#2026", user.PasswordHash));
    }

    private sealed record UserRow(string UserName, string PasswordHash, string Role, bool IsActive);
}
