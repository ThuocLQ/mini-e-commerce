namespace DiscountService.Domain.Discounts;

public sealed class PercentageDiscountStrategy : IDiscountStrategy
{
    public DiscountType Type => DiscountType.Percentage;

    public decimal CalculateDiscount(decimal orderAmount, Coupon coupon)
    {
        if (orderAmount <= 0)
        {
            return 0;
        }

        var percentage = coupon.Value / 100m;
        var discountAmount = orderAmount * percentage;

        return Math.Round(discountAmount, 2);
    }
}