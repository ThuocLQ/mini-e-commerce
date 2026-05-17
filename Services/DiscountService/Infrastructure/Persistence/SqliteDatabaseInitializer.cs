using Dapper;

namespace DiscountService.Infrastructure.Persistence;

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
            CREATE TABLE IF NOT EXISTS Coupons (
                Code TEXT PRIMARY KEY,
                Type TEXT NOT NULL,
                Value REAL NOT NULL,
                ValidFromUtc TEXT NOT NULL,
                ValidToUtc TEXT NOT NULL,
                IsActive INTEGER NOT NULL
            );
            """, cancellationToken: cancellationToken));

        await SeedCouponAsync(connection, "SAVE10", "Percentage", 10, "2024-01-01T00:00:00.0000000Z", "2035-12-31T23:59:59.0000000Z", true, cancellationToken);
        await SeedCouponAsync(connection, "WELCOME50", "FixedAmount", 50, "2024-01-01T00:00:00.0000000Z", "2035-12-31T23:59:59.0000000Z", true, cancellationToken);
        await SeedCouponAsync(connection, "EXPIRED20", "Percentage", 20, "2024-01-01T00:00:00.0000000Z", "2024-12-31T23:59:59.0000000Z", true, cancellationToken);
        await SeedCouponAsync(connection, "DISABLED15", "Percentage", 15, "2024-01-01T00:00:00.0000000Z", "2035-12-31T23:59:59.0000000Z", false, cancellationToken);
    }

    private static async Task SeedCouponAsync(
        System.Data.IDbConnection connection,
        string code,
        string type,
        decimal value,
        string validFromUtc,
        string validToUtc,
        bool isActive,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT OR IGNORE INTO Coupons (Code, Type, Value, ValidFromUtc, ValidToUtc, IsActive)
            VALUES (@Code, @Type, @Value, @ValidFromUtc, @ValidToUtc, @IsActive);
            """, new
        {
            Code = code,
            Type = type,
            Value = value,
            ValidFromUtc = validFromUtc,
            ValidToUtc = validToUtc,
            IsActive = isActive ? 1 : 0
        }, cancellationToken: cancellationToken));
    }
}
