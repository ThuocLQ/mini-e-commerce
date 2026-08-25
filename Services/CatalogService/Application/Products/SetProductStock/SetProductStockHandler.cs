using CatalogService.Application.Abstractions;
using MediatR;

namespace CatalogService.Application.Products.SetProductStock;

public sealed class SetProductStockHandler : IRequestHandler<SetProductStockCommand, ProductDto?>
{
    private readonly IProductRepository _repository;
    private readonly IInventoryStockClient _inventoryClient;

    public SetProductStockHandler(IProductRepository repository, IInventoryStockClient inventoryClient)
    {
        _repository = repository;
        _inventoryClient = inventoryClient;
    }

    public async Task<ProductDto?> Handle(SetProductStockCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Id) || request.StockQuantity < 0)
        {
            throw new ArgumentException("A product id and a non-negative stock quantity are required.");
        }

        var product = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (product is null)
        {
            return null;
        }

        await _inventoryClient.SetStockAsync(product.Id, request.StockQuantity, cancellationToken);
        var updatedSnapshot = await _repository.UpdateStockSnapshotAsync(product.Id, request.StockQuantity, cancellationToken)
            ?? throw new InvalidOperationException("Catalog product disappeared after inventory stock was updated.");

        return ProductMapper.ToDto(updatedSnapshot);
    }
}
