using Dapper;

namespace PaymentService.Infrastructure.Persistence;

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
            CREATE TABLE IF NOT EXISTS Payments (
                Id TEXT PRIMARY KEY,
                OrderId TEXT NOT NULL,
                CustomerId TEXT NOT NULL,
                Amount REAL NOT NULL,
                Currency TEXT NOT NULL,
                Status TEXT NOT NULL,
                ProviderTransactionId TEXT NULL,
                FailureReason TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                CompletedAtUtc TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Payments_OrderId
            ON Payments(OrderId);
            """, cancellationToken: cancellationToken));
    }
}
