using MediatR;

namespace CatalogService.Application.Products.CreateProduct;

public sealed record CreateProductCommand(
    string Name,
    decimal Price,
    string? Description = null,
    int StockQuantity = 0,
    string? Category = null,
    string? ImageUrl = null,
    string? Sku = null,
    string? Brand = null) : IRequest<ProductDto>;