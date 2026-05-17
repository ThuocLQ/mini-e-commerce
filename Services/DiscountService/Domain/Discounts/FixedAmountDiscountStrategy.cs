namespace DiscountService.Domain.Discounts;

public sealed class FixedAmountDiscountStrategy : IDiscountStrategy
{
    public DiscountType Type => DiscountType.FixedAmount;

    public decimal CalculateDiscount(decimal orderAmount, Coupon coupon)
    {
        if (orderAmount <= 0)
        {
            return 0;
        }

        return Math.Min(orderAmount, coupon.Value);
    }
}