using MediatR;

namespace DiscountService.Application.Discounts.GetDiscountByCode;

public sealed record GetDiscountByCodeQuery(string Code) : IRequest<CouponDto?>;