using Microsoft.Extensions.Options;
using OrderingService.Application.Abstractions;
using OrderingService.Application.Baskets;
using OrderingService.Application.IntegrationEvents;

namespace OrderingService.Application.Orders.CheckoutQuote;

public sealed class CheckoutQuoteEvaluator(
    IBasketClient basketClient,
    ICatalogProductSnapshotClient catalogProductClient,
    IDiscountClient discountClient,
    IInventoryAvailabilityClient inventoryAvailabilityClient,
    CheckoutAddressSnapshotResolver addressSnapshotResolver,
    IOptions<OrderEventOptions> orderEventOptions,
    TimeProvider timeProvider)
{
    public async Task<CheckoutQuoteEvaluation> EvaluateAsync(
        CheckoutQuoteRequestBinding request,
        CancellationToken cancellationToken)
    {
        CheckoutRequestValidation.EnsureValidBasketId(request.BasketId);
        CheckoutRequestValidation.EnsureValidBasketVersion(request.BasketVersion);

        var basket = await basketClient.GetBasketAsync(request.CustomerId, cancellationToken);
        CheckoutRequestValidation.EnsureBasketOwnershipAndVersion(
            basket,
            request.CustomerId,
            request.BasketId,
            request.BasketVersion);

        var shippingAddress = await addressSnapshotResolver.ResolveAsync(
            request.CustomerId,
            request.ShippingAddressId,
            cancellationToken);

        var evaluatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var issues = new List<CheckoutQuoteIssueDto>();
        var items = new List<CheckoutQuoteEvaluationItem>(basket!.Items.Count);
        var validInventoryItems = new List<InventoryAvailabilityRequestItem>(basket.Items.Count);

        foreach (var item in basket.Items)
        {
            if (!Guid.TryParse(item.ProductId, out var productId) || productId == Guid.Empty || item.Quantity <= 0)
            {
                issues.Add(new CheckoutQuoteIssueDto(
                    "INVALID_BASKET_ITEM",
                    "Basket contains an invalid product or quantity."));
                items.Add(new CheckoutQuoteEvaluationItem(
                    null,
                    item.ProductName,
                    null,
                    item.Price,
                    null,
                    item.Quantity,
                    false));
                continue;
            }

            var product = await catalogProductClient.GetProductAsync(productId, cancellationToken);
            if (product is null || product.Price < 0 || string.IsNullOrWhiteSpace(product.Name))
            {
                issues.Add(new CheckoutQuoteIssueDto(
                    "PRODUCT_UNAVAILABLE",
                    $"Product '{productId:D}' is no longer available for checkout.",
                    productId));
                items.Add(new CheckoutQuoteEvaluationItem(
                    productId,
                    item.ProductName,
                    null,
                    item.Price,
                    null,
                    item.Quantity,
                    false));
                continue;
            }

            items.Add(new CheckoutQuoteEvaluationItem(
                productId,
                item.ProductName,
                product.Name,
                item.Price,
                product.Price,
                item.Quantity,
                false));
            validInventoryItems.Add(new InventoryAvailabilityRequestItem(productId, item.Quantity));
        }

        if (validInventoryItems.Count == basket.Items.Count)
        {
            var availability = await inventoryAvailabilityClient.GetAvailabilityAsync(validInventoryItems, cancellationToken);
            var availabilityByProductId = availability.ToDictionary(item => item.ProductId);

            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                if (item.ProductId is not { } productId ||
                    !availabilityByProductId.TryGetValue(productId, out var availabilityItem))
                {
                    throw new InvalidOperationException("InventoryService returned an incomplete availability response.");
                }

                items[index] = item with { Available = availabilityItem.Available };
                if (!availabilityItem.Available)
                {
                    issues.Add(new CheckoutQuoteIssueDto(
                        "INSUFFICIENT_INVENTORY",
                        $"Product '{productId:D}' is no longer available in the requested quantity.",
                        productId));
                }
            }
        }

        var subtotalAmount = items.Sum(item => item.CurrentLineTotal ?? 0m);
        var normalizedCouponCode = CheckoutRequestValidation.NormalizeCouponCode(request.CouponCode);
        var coupon = new CheckoutQuoteCouponDto(
            normalizedCouponCode,
            true,
            0m,
            subtotalAmount,
            normalizedCouponCode is null ? "No coupon applied." : "Coupon has not been evaluated.");

        if (normalizedCouponCode is not null)
        {
            if (items.Any(item => item.CurrentUnitPrice is null))
            {
                coupon = coupon with
                {
                    IsValid = false,
                    Message = "Coupon cannot be evaluated until every basket product is available."
                };
                issues.Add(new CheckoutQuoteIssueDto("COUPON_NOT_EVALUATED", coupon.Message));
            }
            else
            {
                var result = await discountClient.ApplyAsync(normalizedCouponCode, subtotalAmount, cancellationToken);
                var isConsistent = result.IsValid &&
                                   result.DiscountAmount >= 0m &&
                                   result.FinalAmount == subtotalAmount - result.DiscountAmount;
                coupon = new CheckoutQuoteCouponDto(
                    result.CouponCode,
                    isConsistent,
                    isConsistent ? result.DiscountAmount : 0m,
                    isConsistent ? result.FinalAmount : subtotalAmount,
                    result.Message);

                if (!isConsistent)
                {
                    issues.Add(new CheckoutQuoteIssueDto("COUPON_INVALID", result.Message));
                }
            }
        }

        return new CheckoutQuoteEvaluation(
            basket,
            shippingAddress,
            items,
            coupon,
            subtotalAmount,
            coupon.DiscountAmount,
            coupon.FinalAmount,
            orderEventOptions.Value.Currency,
            issues,
            evaluatedAtUtc);
    }
}
