using System.Data;
using Microsoft.Data.Sqlite;

namespace CatalogService.Infrastructure.Persistence;

public class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("CatalogDb")
                            ?? throw new InvalidOperationException("Connection string 'CatalogDb' is missing.");
    }
    
    public IDbConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }
}