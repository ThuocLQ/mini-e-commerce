using System.Data;
using Microsoft.Data.Sqlite;

namespace IdentityService.Infrastructure.Persistence;

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("IdentityDb")
                            ?? throw new InvalidOperationException("Connection string 'IdentityDb' is missing.");
    }

    public IDbConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }
}
