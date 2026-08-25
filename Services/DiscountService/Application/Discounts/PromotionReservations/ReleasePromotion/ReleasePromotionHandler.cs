using DiscountService.Application.Abstractions;
using MediatR;
namespace DiscountService.Application.Discounts.PromotionReservations.ReleasePromotion;
public sealed class ReleasePromotionHandler(IPromotionReservationRepository repository) : IRequestHandler<ReleasePromotionCommand, PromotionReservationDto?>
{ public async Task<PromotionReservationDto?> Handle(ReleasePromotionCommand request, CancellationToken cancellationToken) { var result = await repository.ReleaseAsync(request.ReservationId, request.OrderId, request.Reason, cancellationToken); return result is null ? null : PromotionReservationMapper.ToDto(result); } }
