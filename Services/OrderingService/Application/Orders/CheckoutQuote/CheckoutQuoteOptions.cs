namespace OrderingService.Application.Orders.CheckoutQuote;

public sealed class CheckoutQuoteOptions
{
    public const string SectionName = "CheckoutQuote";

    public int LifetimeSeconds { get; set; } = 300;

    // Prefer a dedicated secret. Infrastructure derives a purpose-scoped fallback
    // from the JWT secret to keep existing deployments backward compatible.
    public string? SigningKey { get; set; }
}
