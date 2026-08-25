namespace DiscountService.Domain.Discounts;

public sealed record PromotionReservation(
    Guid Id,
    string CouponCode,
    Guid OrderId,
    Guid CustomerId,
    decimal OrderAmount,
    decimal DiscountAmount,
    decimal FinalAmount,
    PromotionReservationStatus Status,
    DateTime ExpiresAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
