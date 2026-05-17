using MediatR;

namespace DiscountService.Application.Discounts.ApplyDiscount;

public sealed record ApplyDiscountCommand(
    string CouponCode,
    decimal OrderAmount) : IRequest<DiscountResultDto>;