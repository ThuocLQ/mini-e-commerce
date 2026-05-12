using Dapper;

namespace CatalogService.Infrastructure.Persistence;

public sealed class SqliteDatabaseInitializer : IDatabaseInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SqliteDatabaseInitializer(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            CREATE TABLE IF NOT EXISTS Products (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Price REAL NOT NULL DEFAULT 0
            );
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            CREATE INDEX IF NOT EXISTS IX_Products_Name
            ON Products (Name);
            """, cancellationToken: cancellationToken));
    }
}
