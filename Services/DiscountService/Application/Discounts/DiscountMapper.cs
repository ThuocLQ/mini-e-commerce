using DiscountService.Domain.Discounts;

namespace DiscountService.Application.Discounts;

public static class DiscountMapper
{
    public static CouponDto ToDto(Coupon coupon)
    {
        return new CouponDto(
            coupon.Code,
            coupon.Type.ToString(),
            coupon.Value,
            coupon.ValidFromUtc,
            coupon.ValidToUtc,
            coupon.IsActive);
    }
}