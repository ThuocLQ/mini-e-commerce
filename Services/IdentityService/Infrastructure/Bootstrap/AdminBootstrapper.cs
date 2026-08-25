using Dapper;
using IdentityService.Application.Abstractions;
using IdentityService.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace IdentityService.Infrastructure.Bootstrap;

public sealed class AdminBootstrapper : IAdminBootstrapper
{
    private readonly BootstrapAdminOptions _options;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AdminBootstrapper> _logger;

    public AdminBootstrapper(
        IOptions<BootstrapAdminOptions> options,
        IDbConnectionFactory connectionFactory,
        IPasswordHasher passwordHasher,
        ILogger<AdminBootstrapper> logger)
    {
        _options = options.Value;
        _connectionFactory = connectionFactory;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task BootstrapAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var normalizedUserName = _options.UserName.Trim().ToUpperInvariant();
        using var connection = _connectionFactory.CreateConnection();

        var inserted = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO Users (Id, UserName, NormalizedUserName, PasswordHash, Role, IsActive)
            VALUES (@Id, @UserName, @NormalizedUserName, @PasswordHash, 'Admin', true)
            ON CONFLICT (NormalizedUserName) DO NOTHING;
            """, new
        {
            Id = Guid.NewGuid(),
            UserName = _options.UserName.Trim(),
            NormalizedUserName = normalizedUserName,
            PasswordHash = _passwordHasher.Hash(_options.Password)
        }, cancellationToken: cancellationToken));

        _logger.LogInformation(
            inserted == 1
                ? "Created explicitly configured bootstrap administrator {UserName}."
                : "Bootstrap administrator {UserName} already exists.",
            _options.UserName.Trim());
    }
}
