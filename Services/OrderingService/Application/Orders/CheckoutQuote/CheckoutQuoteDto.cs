namespace OrderingService.Application.Orders.CheckoutQuote;

public sealed record CheckoutQuoteDto(
    Guid BasketId,
    long BasketVersion,
    IReadOnlyList<CheckoutQuoteItemDto> Items,
    CheckoutQuoteCouponDto Coupon,
    decimal SubtotalAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    string Currency,
    bool CanCheckout,
    IReadOnlyList<CheckoutQuoteIssueDto> Issues,
    string? QuoteToken,
    DateTime EvaluatedAtUtc,
    DateTime ExpiresAtUtc,
    bool FinalRevalidationRequired);

public sealed record CheckoutQuoteItemDto(
    Guid? ProductId,
    string? BasketProductName,
    string? ProductName,
    decimal BasketUnitPrice,
    decimal? CurrentUnitPrice,
    int Quantity,
    decimal BasketLineTotal,
    decimal? CurrentLineTotal,
    bool PriceChanged,
    bool Availability);

public sealed record CheckoutQuoteCouponDto(
    string? CouponCode,
    bool IsValid,
    decimal DiscountAmount,
    decimal FinalAmount,
    string Message);

public sealed record CheckoutQuoteIssueDto(
    string Code,
    string Message,
    Guid? ProductId = null);
