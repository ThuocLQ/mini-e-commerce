namespace OrderingService.Application.Baskets;

public sealed class BasketUnavailableException : Exception
{
    public BasketUnavailableException()
        : base("BasketService is unavailable. Please try again later.")
    {
    }

    public BasketUnavailableException(Exception innerException)
        : base("BasketService is unavailable. Please try again later.", innerException)
    {
    }
}
