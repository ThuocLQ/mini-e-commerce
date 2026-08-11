using MediatR;

namespace CatalogService.Application.Products.CreateProduct;

public sealed record CreateProductCommand(string Name, decimal Price, string? Description = null, int StockQuantity = 0) : IRequest<ProductDto>;
