using Dapper;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Users;

namespace IdentityService.Infrastructure.Persistence;

public sealed class DapperUserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperUserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AppUser?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition("""
            SELECT Id, UserName, PasswordHash, Role, IsActive
            FROM Users
            WHERE NormalizedUserName = @NormalizedUserName;
            """, new
        {
            NormalizedUserName = userName.Trim().ToUpperInvariant()
        }, cancellationToken: cancellationToken));

        return row is null
            ? null
            : new AppUser(
                Guid.Parse(row.Id),
                row.UserName,
                row.PasswordHash,
                row.Role,
                row.IsActive == 1);
    }

    private sealed record UserRow(
        string Id,
        string UserName,
        string PasswordHash,
        string Role,
        long IsActive);
}
