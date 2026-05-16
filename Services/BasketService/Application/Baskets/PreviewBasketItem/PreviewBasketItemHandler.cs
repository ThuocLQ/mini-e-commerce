using BasketService.Application.Abstractions;
using MediatR;

namespace BasketService.Application.Baskets.PreviewBasketItem;

public sealed class PreviewBasketItemHandler : IRequestHandler<PreviewBasketItemCommand, PreviewBasketItemResult?>
{
    private readonly ICatalogProductClient _catalogProductClient;

    public PreviewBasketItemHandler(ICatalogProductClient catalogProductClient)
    {
        _catalogProductClient = catalogProductClient;
    }

    public async Task<PreviewBasketItemResult?> Handle(
        PreviewBasketItemCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProductId))
        {
            throw new ArgumentException("ProductId is required.");
        }

        if (request.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than 0.");
        }

        var product = await _catalogProductClient.GetProductByIdAsync(
            request.ProductId,
            request.Mode,
            cancellationToken);

        if (product is null)
        {
            return null;
        }

        return new PreviewBasketItemResult(
            product.Id,
            product.Name,
            request.Quantity,
            product.Price,
            product.Description);
    }
}
