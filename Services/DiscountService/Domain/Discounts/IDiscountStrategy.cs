namespace DiscountService.Domain.Discounts;

public interface IDiscountStrategy
{
    DiscountType Type { get; }

    decimal CalculateDiscount(decimal orderAmount, Coupon coupon);
}