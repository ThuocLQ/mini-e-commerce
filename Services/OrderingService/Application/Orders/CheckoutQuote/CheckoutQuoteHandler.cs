using MediatR;
using Microsoft.Extensions.Options;

namespace OrderingService.Application.Orders.CheckoutQuote;

public sealed class CheckoutQuoteHandler(
    CheckoutQuoteEvaluator evaluator,
    ICheckoutQuoteTokenService quoteTokenService,
    IOptions<CheckoutQuoteOptions> options)
    : IRequestHandler<CheckoutQuoteCommand, CheckoutQuoteDto>
{
    public async Task<CheckoutQuoteDto> Handle(CheckoutQuoteCommand request, CancellationToken cancellationToken)
    {
        var evaluation = await evaluator.EvaluateAsync(
            new CheckoutQuoteRequestBinding(
                request.CustomerId,
                request.BasketId,
                request.BasketVersion,
                request.CouponCode,
                request.ShippingAddressId),
            cancellationToken);

        var expiresAtUtc = evaluation.EvaluatedAtUtc.AddSeconds(options.Value.LifetimeSeconds);
        var token = evaluation.CanCheckout
            ? quoteTokenService.Create(CreateTokenPayload(request, evaluation, expiresAtUtc))
            : null;

        return new CheckoutQuoteDto(
            evaluation.Basket.BasketId,
            evaluation.Basket.Version,
            evaluation.Items.Select(MapItem).ToList(),
            evaluation.Coupon,
            evaluation.SubtotalAmount,
            evaluation.DiscountAmount,
            evaluation.TotalAmount,
            evaluation.Currency,
            evaluation.CanCheckout,
            evaluation.Issues,
            token,
            evaluation.EvaluatedAtUtc,
            expiresAtUtc,
            true);
    }

    private static CheckoutQuoteTokenPayload CreateTokenPayload(
        CheckoutQuoteCommand request,
        CheckoutQuoteEvaluation evaluation,
        DateTime expiresAtUtc) =>
        new(
            1,
            request.CustomerId,
            evaluation.Basket.BasketId,
            evaluation.Basket.Version,
            CheckoutRequestValidation.NormalizeCouponCode(request.CouponCode),
            request.ShippingAddressId,
            evaluation.Items.Select(item => new CheckoutQuoteTokenItem(
                item.ProductId!.Value,
                item.ProductName!,
                item.CurrentUnitPrice!.Value,
                item.Quantity)).OrderBy(item => item.ProductId).ToList(),
            evaluation.SubtotalAmount,
            evaluation.DiscountAmount,
            evaluation.TotalAmount,
            evaluation.Currency,
            evaluation.EvaluatedAtUtc,
            expiresAtUtc);

    private static CheckoutQuoteItemDto MapItem(CheckoutQuoteEvaluationItem item) =>
        new(
            item.ProductId,
            item.BasketProductName,
            item.ProductName,
            item.BasketUnitPrice,
            item.CurrentUnitPrice,
            item.Quantity,
            item.BasketLineTotal,
            item.CurrentLineTotal,
            item.PriceChanged,
            item.Available);
}
