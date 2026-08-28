namespace OrderingService.Application.Orders.CheckoutQuote;

public static class CheckoutQuoteSnapshotVerifier
{
    public static void EnsureCurrentState(CheckoutQuoteTokenPayload token, CheckoutQuoteEvaluation evaluation)
    {
        if (!evaluation.CanCheckout)
        {
            throw new CheckoutQuoteConflictException(
                "Checkout quote is stale because the current basket, coupon, or inventory state changed.");
        }

        var currentItems = evaluation.Items
            .Select(item => new CheckoutQuoteTokenItem(
                item.ProductId!.Value,
                item.ProductName!,
                item.CurrentUnitPrice!.Value,
                item.Quantity))
            .OrderBy(item => item.ProductId)
            .ToArray();

        if (token.Items.Count != currentItems.Length ||
            !token.Items.OrderBy(item => item.ProductId).SequenceEqual(currentItems) ||
            token.SubtotalAmount != evaluation.SubtotalAmount ||
            token.DiscountAmount != evaluation.DiscountAmount ||
            token.TotalAmount != evaluation.TotalAmount ||
            !string.Equals(token.Currency, evaluation.Currency, StringComparison.Ordinal))
        {
            throw new CheckoutQuoteConflictException(
                "Checkout quote is stale because pricing or discount state changed.");
        }
    }
}