using Dapper;
using DiscountService.Application.Abstractions;
using DiscountService.Domain.Discounts;

namespace DiscountService.Infrastructure.Persistence;

public sealed class DapperPromotionReservationRepository : IPromotionReservationRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly DiscountStrategyFactory _strategyFactory;

    public DapperPromotionReservationRepository(IDbConnectionFactory connectionFactory, DiscountStrategyFactory strategyFactory)
    {
        _connectionFactory = connectionFactory;
        _strategyFactory = strategyFactory;
    }

    public async Task<PromotionReservationResult> ReserveAsync(
        string couponCode,
        Guid orderId,
        Guid customerId,
        decimal orderAmount,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(couponCode)) throw new ArgumentException("Coupon code is required.", nameof(couponCode));
        if (orderId == Guid.Empty || customerId == Guid.Empty) throw new ArgumentException("Order and customer identifiers are required.");
        if (orderAmount <= 0) throw new ArgumentOutOfRangeException(nameof(orderAmount));
        if (expiresAtUtc <= DateTime.UtcNow) throw new ArgumentException("Reservation expiry must be in the future.", nameof(expiresAtUtc));

        var normalizedCode = couponCode.Trim().ToUpperInvariant();
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var nowUtc = DateTime.UtcNow;
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE PromotionReservations
                SET Status = 'Expired', UpdatedAtUtc = @NowUtc
                WHERE CouponCode = @CouponCode AND Status = 'Reserved' AND ExpiresAtUtc <= @NowUtc;
                """, new { CouponCode = normalizedCode, NowUtc = nowUtc }, transaction, cancellationToken: cancellationToken));

            var coupon = await connection.QuerySingleOrDefaultAsync<CouponRow>(new CommandDefinition("""
                SELECT Code, Type, Value, ValidFromUtc, ValidToUtc, IsActive, MaxRedemptions, RedemptionCount
                FROM Coupons WHERE Code = @CouponCode FOR UPDATE;
                """, new { CouponCode = normalizedCode }, transaction, cancellationToken: cancellationToken));
            if (coupon is null)
            {
                transaction.Commit();
                return new PromotionReservationResult(false, null, "Coupon was not found.");
            }

            var existing = await GetByCouponAndOrderAsync(normalizedCode, orderId, transaction, cancellationToken);
            if (existing is not null)
            {
                transaction.Commit();
                return existing.Status == PromotionReservationStatus.Reserved && existing.ExpiresAtUtc > nowUtc
                    ? new PromotionReservationResult(true, existing, "Coupon reservation already exists.")
                    : new PromotionReservationResult(false, existing, $"Coupon reservation is already {existing.Status}.");
            }

            var aggregate = MapCoupon(coupon);
            if (!aggregate.CanBeUsedAt(nowUtc) || !aggregate.HasRemainingCapacity())
            {
                transaction.Commit();
                return new PromotionReservationResult(false, null, "Coupon is inactive, expired, or exhausted.");
            }

            var activeReservations = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM PromotionReservations
                WHERE CouponCode = @CouponCode AND Status = 'Reserved' AND ExpiresAtUtc > @NowUtc;
                """, new { CouponCode = normalizedCode, NowUtc = nowUtc }, transaction, cancellationToken: cancellationToken));
            if (aggregate.MaxRedemptions is { } limit && aggregate.RedemptionCount + activeReservations >= limit)
            {
                transaction.Commit();
                return new PromotionReservationResult(false, null, "Coupon redemption limit has been reached.");
            }

            var discountAmount = _strategyFactory.GetStrategy(aggregate.Type).CalculateDiscount(orderAmount, aggregate);
            var reservation = new PromotionReservation(Guid.NewGuid(), aggregate.Code, orderId, customerId, orderAmount,
                discountAmount, Math.Max(0, orderAmount - discountAmount), PromotionReservationStatus.Reserved,
                expiresAtUtc, nowUtc, nowUtc);
            await InsertAsync(reservation, transaction, cancellationToken);
            transaction.Commit();
            return new PromotionReservationResult(true, reservation, "Coupon reserved.");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public Task<PromotionReservation?> RedeemAsync(Guid reservationId, Guid orderId, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(reservationId, orderId, PromotionReservationStatus.Redeemed, null, cancellationToken);

    public Task<PromotionReservation?> ReleaseAsync(Guid reservationId, Guid orderId, string reason, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(reservationId, orderId, PromotionReservationStatus.Released, reason, cancellationToken);

    private async Task<PromotionReservation?> ChangeStatusAsync(Guid reservationId, Guid orderId, PromotionReservationStatus targetStatus, string? reason, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var reservation = await connection.QuerySingleOrDefaultAsync<ReservationRow>(new CommandDefinition("""
                SELECT Id, CouponCode, OrderId, CustomerId, OrderAmount, DiscountAmount, FinalAmount, Status, ExpiresAtUtc, CreatedAtUtc, UpdatedAtUtc
                FROM PromotionReservations WHERE Id = @ReservationId AND OrderId = @OrderId FOR UPDATE;
                """, new { ReservationId = reservationId, OrderId = orderId }, transaction, cancellationToken: cancellationToken));
            if (reservation is null) { transaction.Commit(); return null; }

            var current = MapReservation(reservation);
            if (current.Status == targetStatus) { transaction.Commit(); return current; }
            if (current.Status != PromotionReservationStatus.Reserved)
            {
                transaction.Commit();
                return current;
            }

            if (targetStatus == PromotionReservationStatus.Redeemed)
            {
                if (current.ExpiresAtUtc <= DateTime.UtcNow) { transaction.Commit(); return current; }
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE Coupons SET RedemptionCount = RedemptionCount + 1 WHERE Code = @CouponCode;
                    """, new { current.CouponCode }, transaction, cancellationToken: cancellationToken));
            }

            var updatedAtUtc = DateTime.UtcNow;
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE PromotionReservations SET Status = @Status, UpdatedAtUtc = @UpdatedAtUtc, ReleaseReason = @Reason
                WHERE Id = @ReservationId;
                """, new { ReservationId = reservationId, Status = targetStatus.ToString(), UpdatedAtUtc = updatedAtUtc, Reason = reason }, transaction, cancellationToken: cancellationToken));
            transaction.Commit();
            return current with { Status = targetStatus, UpdatedAtUtc = updatedAtUtc };
        }
        catch { transaction.Rollback(); throw; }
    }

    private static async Task<PromotionReservation?> GetByCouponAndOrderAsync(string couponCode, Guid orderId, System.Data.IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var row = await transaction.Connection!.QuerySingleOrDefaultAsync<ReservationRow>(new CommandDefinition("""
            SELECT Id, CouponCode, OrderId, CustomerId, OrderAmount, DiscountAmount, FinalAmount, Status, ExpiresAtUtc, CreatedAtUtc, UpdatedAtUtc
            FROM PromotionReservations WHERE CouponCode = @CouponCode AND OrderId = @OrderId FOR UPDATE;
            """, new { CouponCode = couponCode, OrderId = orderId }, transaction, cancellationToken: cancellationToken));
        return row is null ? null : MapReservation(row);
    }

    private static Task InsertAsync(PromotionReservation value, System.Data.IDbTransaction transaction, CancellationToken cancellationToken) =>
        transaction.Connection!.ExecuteAsync(new CommandDefinition("""
            INSERT INTO PromotionReservations (Id, CouponCode, OrderId, CustomerId, OrderAmount, DiscountAmount, FinalAmount, Status, ExpiresAtUtc, CreatedAtUtc, UpdatedAtUtc)
            VALUES (@Id, @CouponCode, @OrderId, @CustomerId, @OrderAmount, @DiscountAmount, @FinalAmount, @Status, @ExpiresAtUtc, @CreatedAtUtc, @UpdatedAtUtc);
            """, new { value.Id, value.CouponCode, value.OrderId, value.CustomerId, value.OrderAmount, value.DiscountAmount, value.FinalAmount, Status = value.Status.ToString(), value.ExpiresAtUtc, value.CreatedAtUtc, value.UpdatedAtUtc }, transaction, cancellationToken: cancellationToken));

    private static Coupon MapCoupon(CouponRow row) => new(row.Code, Enum.Parse<DiscountType>(row.Type), row.Value, row.ValidFromUtc, row.ValidToUtc, row.IsActive, row.MaxRedemptions, row.RedemptionCount);
    private static PromotionReservation MapReservation(ReservationRow row) => new(row.Id, row.CouponCode, row.OrderId, row.CustomerId, row.OrderAmount, row.DiscountAmount, row.FinalAmount, Enum.Parse<PromotionReservationStatus>(row.Status), row.ExpiresAtUtc, row.CreatedAtUtc, row.UpdatedAtUtc);
    private sealed record CouponRow(string Code, string Type, decimal Value, DateTime ValidFromUtc, DateTime ValidToUtc, bool IsActive, int? MaxRedemptions, int RedemptionCount);
    private sealed record ReservationRow(Guid Id, string CouponCode, Guid OrderId, Guid CustomerId, decimal OrderAmount, decimal DiscountAmount, decimal FinalAmount, string Status, DateTime ExpiresAtUtc, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
}
