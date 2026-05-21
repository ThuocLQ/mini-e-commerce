using Dapper;
using DiscountService.Application.Abstractions;
using DiscountService.Domain.Discounts;

namespace DiscountService.Infrastructure.Persistence;

public sealed class DapperDiscountRepository : IDiscountRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperDiscountRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Coupon?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<CouponRow>(new CommandDefinition("""
            SELECT Code, Type, Value, ValidFromUtc, ValidToUtc, IsActive
            FROM Coupons
            WHERE Code = @Code;
            """, new
        {
            Code = code.Trim().ToUpperInvariant()
        }, cancellationToken: cancellationToken));

        return row is null ? null : MapCoupon(row);
    }

    private static Coupon MapCoupon(CouponRow row)
    {
        return new Coupon(
            row.Code,
            Enum.Parse<DiscountType>(row.Type),
            row.Value,
            row.ValidFromUtc,
            row.ValidToUtc,
            row.IsActive);
    }

    private sealed record CouponRow(
        string Code,
        string Type,
        decimal Value,
        DateTime ValidFromUtc,
        DateTime ValidToUtc,
        bool IsActive);
}
