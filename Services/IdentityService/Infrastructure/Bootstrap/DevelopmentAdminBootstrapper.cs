using Dapper;
using IdentityService.Application.Abstractions;
using IdentityService.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace IdentityService.Infrastructure.Bootstrap;

public sealed class DevelopmentAdminBootstrapper : IAdminBootstrapper
{
    private readonly BootstrapAdminOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DevelopmentAdminBootstrapper> _logger;

    public DevelopmentAdminBootstrapper(
        IOptions<BootstrapAdminOptions> options,
        IHostEnvironment environment,
        IDbConnectionFactory connectionFactory,
        IPasswordHasher passwordHasher,
        ILogger<DevelopmentAdminBootstrapper> logger)
    {
        _options = options.Value;
        _environment = environment;
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

        if (!_environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "BootstrapAdmin may only be enabled in the Development environment.");
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
                ? "Created explicitly configured development bootstrap administrator {UserName}."
                : "Development bootstrap administrator {UserName} already exists.",
            _options.UserName.Trim());
    }
}
