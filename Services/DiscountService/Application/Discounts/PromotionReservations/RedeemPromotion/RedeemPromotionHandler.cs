using DiscountService.Application.Abstractions;
using MediatR;
namespace DiscountService.Application.Discounts.PromotionReservations.RedeemPromotion;
public sealed class RedeemPromotionHandler(IPromotionReservationRepository repository) : IRequestHandler<RedeemPromotionCommand, PromotionReservationDto?>
{ public async Task<PromotionReservationDto?> Handle(RedeemPromotionCommand request, CancellationToken cancellationToken) { var result = await repository.RedeemAsync(request.ReservationId, request.OrderId, cancellationToken); return result is null ? null : PromotionReservationMapper.ToDto(result); } }
