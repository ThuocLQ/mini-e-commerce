using DiscountService.Application.Abstractions;
using DiscountService.Domain.Discounts;
using MediatR;

namespace DiscountService.Application.Discounts.ApplyDiscount;

public sealed class ApplyDiscountHandler : IRequestHandler<ApplyDiscountCommand, DiscountResultDto>
{
    private readonly IDiscountRepository _repository;
    private readonly DiscountStrategyFactory _strategyFactory;

    public ApplyDiscountHandler(
        IDiscountRepository repository,
        DiscountStrategyFactory strategyFactory)
    {
        _repository = repository;
        _strategyFactory = strategyFactory;
    }

    public async Task<DiscountResultDto> Handle(ApplyDiscountCommand request, CancellationToken cancellationToken)
    {
        if (request.OrderAmount <= 0)
        {
            return new DiscountResultDto(
                request.CouponCode,
                false,
                request.OrderAmount,
                0,
                request.OrderAmount,
                "Order amount must be greater than zero.");
        }

        var coupon = await _repository.GetByCodeAsync(request.CouponCode, cancellationToken);

        if (coupon is null)
        {
            return new DiscountResultDto(
                request.CouponCode,
                false,
                request.OrderAmount,
                0,
                request.OrderAmount,
                "Coupon was not found.");
        }

        if (!coupon.CanBeUsedAt(DateTime.UtcNow))
        {
            return new DiscountResultDto(
                coupon.Code,
                false,
                request.OrderAmount,
                0,
                request.OrderAmount,
                "Coupon is expired or inactive.");
        }

        var strategy = _strategyFactory.GetStrategy(coupon.Type);
        var discountAmount = strategy.CalculateDiscount(request.OrderAmount, coupon);
        var finalAmount = Math.Max(0, request.OrderAmount - discountAmount);

        return new DiscountResultDto(
            coupon.Code,
            true,
            request.OrderAmount,
            discountAmount,
            finalAmount,
            "Coupon applied.");
    }
}