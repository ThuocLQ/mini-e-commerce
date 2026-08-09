namespace BasketService.Application.Baskets;

public sealed class BasketConcurrencyException : Exception
{
    public BasketConcurrencyException()
        : base("Basket was changed by another request. Reload the basket and try again.")
    {
    }
}
