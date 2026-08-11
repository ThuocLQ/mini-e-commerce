using CatalogService.Application.Abstractions;
using MediatR;

namespace CatalogService.Application.Products.SetProductStock;

public sealed class SetProductStockHandler : IRequestHandler<SetProductStockCommand, ProductDto?>
{
    private readonly IProductRepository _repository;
    public SetProductStockHandler(IProductRepository repository) => _repository = repository;

    public async Task<ProductDto?> Handle(SetProductStockCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Id) || request.StockQuantity < 0)
        {
            throw new ArgumentException("A product id and a non-negative stock quantity are required.");
        }

        var product = await _repository.SetStockQuantityAsync(request.Id, request.StockQuantity, cancellationToken);
        if (product is null)
        {
            throw new InvalidOperationException("Product was not found or the requested stock is below the currently reserved quantity.");
        }

        return ProductMapper.ToDto(product);
    }
}
