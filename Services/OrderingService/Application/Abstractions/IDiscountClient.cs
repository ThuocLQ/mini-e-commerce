namespace OrderingService.Application.Abstractions;

public interface IDiscountClient
{
    Task<DiscountApplicationResult> ApplyAsync(
        string couponCode,
        decimal orderAmount,
        CancellationToken cancellationToken = default);

    Task<DiscountReservationResult> ReserveAsync(string couponCode, Guid orderId, Guid customerId, decimal orderAmount, DateTime expiresAtUtc, CancellationToken cancellationToken = default);
    Task RedeemAsync(Guid reservationId, Guid orderId, CancellationToken cancellationToken = default);
    Task ReleaseAsync(Guid reservationId, Guid orderId, string reason, CancellationToken cancellationToken = default);
}

public sealed record DiscountApplicationResult(
    string CouponCode,
    bool IsValid,
    decimal DiscountAmount,
    decimal FinalAmount,
    string Message);

public sealed record DiscountReservationResult(bool IsReserved, Guid? ReservationId, string CouponCode, decimal DiscountAmount, decimal FinalAmount, string Message);
