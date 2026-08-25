using MediatR;

namespace DiscountService.Application.Discounts.PromotionReservations.ReservePromotion;

public sealed record ReservePromotionCommand(string CouponCode, Guid OrderId, Guid CustomerId, decimal OrderAmount, DateTime ExpiresAtUtc)
    : IRequest<ReservePromotionResult>;

public sealed record ReservePromotionResult(bool IsReserved, PromotionReservationDto? Reservation, string Message);
