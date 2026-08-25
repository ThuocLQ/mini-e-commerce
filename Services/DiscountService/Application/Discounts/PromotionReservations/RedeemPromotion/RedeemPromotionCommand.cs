using MediatR;
namespace DiscountService.Application.Discounts.PromotionReservations.RedeemPromotion;
public sealed record RedeemPromotionCommand(Guid ReservationId, Guid OrderId) : IRequest<PromotionReservationDto?>;
