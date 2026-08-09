namespace OrderingService.Application.Discounts;

public sealed class DiscountUnavailableException : Exception
{
    public DiscountUnavailableException(Exception innerException)
        : base("DiscountService is unavailable. Checkout cannot validate the coupon.", innerException)
    {
    }
}
