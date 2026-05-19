using System.Data;
using Microsoft.Data.Sqlite;

namespace PaymentService.Infrastructure.Persistence;

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PaymentDb")
                            ?? throw new InvalidOperationException("Connection string 'PaymentDb' is missing.");
    }

    public IDbConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }
}
