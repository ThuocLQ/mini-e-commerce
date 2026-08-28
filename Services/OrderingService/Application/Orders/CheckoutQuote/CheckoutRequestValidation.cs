using OrderingService.Application.Baskets;

namespace OrderingService.Application.Orders.CheckoutQuote;

public static class CheckoutRequestValidation
{
    public static void EnsureValidBasketId(Guid basketId)
    {
        if (basketId == Guid.Empty)
        {
            throw new ArgumentException("BasketId cannot be empty.", nameof(basketId));
        }
    }

    public static void EnsureValidBasketVersion(long basketVersion)
    {
        if (basketVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(basketVersion), "BasketVersion must be greater than zero.");
        }
    }

    public static void EnsureBasketOwnershipAndVersion(BasketDto? basket, Guid customerId, Guid basketId, long basketVersion)
    {
        if (basket is null || basket.Items is not { Count: > 0 })
        {
            throw new InvalidOperationException("Basket is empty.");
        }

        if (!string.Equals(basket.UserId, customerId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Basket does not belong to the authenticated customer.");
        }

        if (basket.BasketId != basketId || basket.Version != basketVersion)
        {
            throw new CheckoutQuoteConflictException(
                "Basket changed before checkout. Refresh the basket and request a new quote.",
                "CHECKOUT_QUOTE_BASKET_CHANGED");
        }
    }

    public static string? NormalizeCouponCode(string? couponCode) =>
        string.IsNullOrWhiteSpace(couponCode) ? null : couponCode.Trim().ToUpperInvariant();
}
