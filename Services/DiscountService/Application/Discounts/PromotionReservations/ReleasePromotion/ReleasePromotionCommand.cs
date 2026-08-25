using MediatR;
namespace DiscountService.Application.Discounts.PromotionReservations.ReleasePromotion;
public sealed record ReleasePromotionCommand(Guid ReservationId, Guid OrderId, string Reason) : IRequest<PromotionReservationDto?>;
