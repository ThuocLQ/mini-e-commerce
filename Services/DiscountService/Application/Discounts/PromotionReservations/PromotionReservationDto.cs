using DiscountService.Domain.Discounts;

namespace DiscountService.Application.Discounts.PromotionReservations;

public sealed record PromotionReservationDto(
    Guid Id,
    string CouponCode,
    Guid OrderId,
    decimal DiscountAmount,
    decimal FinalAmount,
    string Status,
    DateTime ExpiresAtUtc);

internal static class PromotionReservationMapper
{
    public static PromotionReservationDto ToDto(PromotionReservation reservation) => new(
        reservation.Id, reservation.CouponCode, reservation.OrderId, reservation.DiscountAmount,
        reservation.FinalAmount, reservation.Status.ToString(), reservation.ExpiresAtUtc);
}
