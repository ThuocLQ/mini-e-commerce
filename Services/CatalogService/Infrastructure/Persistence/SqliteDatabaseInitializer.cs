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
                Description TEXT NOT NULL DEFAULT '',
                Price REAL NOT NULL DEFAULT 0
            );
            """, cancellationToken: cancellationToken));

        var columns = await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT name
            FROM pragma_table_info('Products');
            """, cancellationToken: cancellationToken));

        if (!columns.Contains("Description", StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                ALTER TABLE Products
                ADD COLUMN Description TEXT NOT NULL DEFAULT '';
                """, cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            CREATE INDEX IF NOT EXISTS IX_Products_Name
            ON Products (Name);
            """, cancellationToken: cancellationToken));
    }
}
