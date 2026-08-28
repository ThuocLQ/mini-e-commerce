using OrderingService.Application.Baskets;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Orders.CheckoutQuote;

public sealed record CheckoutQuoteEvaluation(
    BasketDto Basket,
    OrderAddressSnapshot? ShippingAddress,
    IReadOnlyList<CheckoutQuoteEvaluationItem> Items,
    CheckoutQuoteCouponDto Coupon,
    decimal SubtotalAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<CheckoutQuoteIssueDto> Issues,
    DateTime EvaluatedAtUtc)
{
    public bool CanCheckout => Issues.Count == 0;
}

public sealed record CheckoutQuoteEvaluationItem(
    Guid? ProductId,
    string? BasketProductName,
    string? ProductName,
    decimal BasketUnitPrice,
    decimal? CurrentUnitPrice,
    int Quantity,
    bool Available)
{
    public decimal BasketLineTotal => BasketUnitPrice * Quantity;

    public decimal? CurrentLineTotal => CurrentUnitPrice * Quantity;

    public bool PriceChanged => CurrentUnitPrice is not null && BasketUnitPrice != CurrentUnitPrice;
}
