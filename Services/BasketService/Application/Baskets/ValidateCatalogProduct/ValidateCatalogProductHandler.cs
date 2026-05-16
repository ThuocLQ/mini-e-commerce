using BasketService.Application.Abstractions;
using MediatR;

namespace BasketService.Application.Baskets.ValidateCatalogProduct;

public sealed class ValidateCatalogProductHandler : IRequestHandler<ValidateCatalogProductQuery, CatalogProductValidateResult>
{
    private readonly ICatalogProductClient _catalogProductClient;

    public ValidateCatalogProductHandler(ICatalogProductClient catalogProductClient)
    {
        _catalogProductClient = catalogProductClient;
    }

    public async Task<CatalogProductValidateResult> Handle(
        ValidateCatalogProductQuery request,
        CancellationToken cancellationToken)
    {
        var product = await _catalogProductClient.GetProductByIdAsync(
            request.ProductId,
            request.Mode,
            cancellationToken);

        if (product is null)
        {
            return new CatalogProductValidateResult(
                Valid: false,
                Message: "Product not found.",
                ProductId: null,
                ProductName: null,
                Price: 0,
                Description: null);
        }

        return new CatalogProductValidateResult(
            Valid: true,
            Message: null,
            ProductId: product.Id,
            ProductName: product.Name,
            Price: product.Price,
            Description: product.Description);
    }
}
