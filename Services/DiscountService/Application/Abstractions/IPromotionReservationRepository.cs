using DiscountService.Domain.Discounts;

namespace DiscountService.Application.Abstractions;

public interface IPromotionReservationRepository
{
    Task<PromotionReservationResult> ReserveAsync(
        string couponCode,
        Guid orderId,
        Guid customerId,
        decimal orderAmount,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);

    Task<PromotionReservation?> RedeemAsync(Guid reservationId, Guid orderId, CancellationToken cancellationToken = default);
    Task<PromotionReservation?> ReleaseAsync(Guid reservationId, Guid orderId, string reason, CancellationToken cancellationToken = default);
}

public sealed record PromotionReservationResult(
    bool IsReserved,
    PromotionReservation? Reservation,
    string Message);
