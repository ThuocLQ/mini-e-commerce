using DiscountService.Application.Abstractions;
using MediatR;

namespace DiscountService.Application.Discounts.GetDiscountByCode;

public sealed class GetDiscountByCodeHandler : IRequestHandler<GetDiscountByCodeQuery, CouponDto?>
{
    private readonly IDiscountRepository _repository;

    public GetDiscountByCodeHandler(IDiscountRepository repository)
    {
        _repository = repository;
    }

    public async Task<CouponDto?> Handle(GetDiscountByCodeQuery request, CancellationToken cancellationToken)
    {
        var coupon = await _repository.GetByCodeAsync(request.Code, cancellationToken);

        return coupon is null
            ? null
            : DiscountMapper.ToDto(coupon);
    }
}