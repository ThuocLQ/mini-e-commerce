using CatalogService.Application.Products;
using MediatR;

namespace CatalogService.Application.Products.UpdateProduct;

public sealed record UpdateProductCommand(
    string Id,
    string Name,
    decimal Price,
    string? Description = null,
    string? Category = null,
    string? ImageUrl = null,
    string? Brand = null,
    string? Sku = null) : IRequest<ProductDto?>;