using System.Data;
using Microsoft.Data.Sqlite;

namespace OrderingService.Infrastructure.Persistence;

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("OrderingDb")
                            ?? throw new InvalidOperationException("Connection string 'OrderingDb' is missing.");
    }

    public IDbConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }
}
