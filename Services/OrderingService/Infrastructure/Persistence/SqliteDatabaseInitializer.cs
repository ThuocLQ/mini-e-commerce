using Dapper;

namespace OrderingService.Infrastructure.Persistence;

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
            CREATE TABLE IF NOT EXISTS Orders (
                Id TEXT PRIMARY KEY,
                CustomerId TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                Status TEXT NOT NULL,
                TotalAmount REAL NOT NULL,
                IdempotencyKey TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS OrderItems (
                Id TEXT PRIMARY KEY,
                OrderId TEXT NOT NULL,
                ProductId TEXT NOT NULL,
                ProductName TEXT NOT NULL,
                UnitPrice REAL NOT NULL,
                Quantity INTEGER NOT NULL,
                TotalPrice REAL NOT NULL,
                FOREIGN KEY (OrderId) REFERENCES Orders(Id)
            );
            """, cancellationToken: cancellationToken));

        var orderColumns = (await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT name
            FROM pragma_table_info('Orders');
            """, cancellationToken: cancellationToken))).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!orderColumns.Contains("IdempotencyKey"))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                ALTER TABLE Orders ADD COLUMN IdempotencyKey TEXT NULL;
                """, cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Orders_CustomerId_IdempotencyKey
            ON Orders(CustomerId, IdempotencyKey)
            WHERE IdempotencyKey IS NOT NULL;
            """, cancellationToken: cancellationToken));
    }
}
