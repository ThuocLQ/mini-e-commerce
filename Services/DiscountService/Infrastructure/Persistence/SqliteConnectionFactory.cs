using System.Data;
using Microsoft.Data.Sqlite;

namespace DiscountService.Infrastructure.Persistence;

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DiscountDb")
                            ?? throw new InvalidOperationException("Connection string 'DiscountDb' is missing.");
    }

    public IDbConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }
}
