using Dapper;
using IdentityService.Application.Abstractions;

namespace IdentityService.Infrastructure.Persistence;

public sealed class SqliteDatabaseInitializer : IDatabaseInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IPasswordHasher _passwordHasher;

    public SqliteDatabaseInitializer(
        IDbConnectionFactory connectionFactory,
        IPasswordHasher passwordHasher)
    {
        _connectionFactory = connectionFactory;
        _passwordHasher = passwordHasher;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY,
                UserName TEXT NOT NULL,
                NormalizedUserName TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL,
                Role TEXT NOT NULL,
                IsActive INTEGER NOT NULL
            );
            """, cancellationToken: cancellationToken));

        var adminExists = await connection.ExecuteScalarAsync<long>(new CommandDefinition("""
            SELECT COUNT(1)
            FROM Users
            WHERE NormalizedUserName = @NormalizedUserName;
            """, new
        {
            NormalizedUserName = "ADMIN"
        }, cancellationToken: cancellationToken));

        if (adminExists > 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO Users (Id, UserName, NormalizedUserName, PasswordHash, Role, IsActive)
            VALUES (@Id, @UserName, @NormalizedUserName, @PasswordHash, @Role, @IsActive);
            """, new
        {
            Id = "11111111-1111-1111-1111-111111111111",
            UserName = "admin",
            NormalizedUserName = "ADMIN",
            PasswordHash = _passwordHasher.Hash("Admin@123"),
            Role = "Admin",
            IsActive = 1
        }, cancellationToken: cancellationToken));
    }
}
