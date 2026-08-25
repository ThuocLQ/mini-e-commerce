using DiscountService.Application.Abstractions;
using MediatR;

namespace DiscountService.Application.Discounts.PromotionReservations.ReservePromotion;

public sealed class ReservePromotionHandler(IPromotionReservationRepository repository)
    : IRequestHandler<ReservePromotionCommand, ReservePromotionResult>
{
    public async Task<ReservePromotionResult> Handle(ReservePromotionCommand request, CancellationToken cancellationToken)
    {
        var result = await repository.ReserveAsync(request.CouponCode, request.OrderId, request.CustomerId, request.OrderAmount, request.ExpiresAtUtc, cancellationToken);
        return new ReservePromotionResult(result.IsReserved, result.Reservation is null ? null : PromotionReservationMapper.ToDto(result.Reservation), result.Message);
    }
}
