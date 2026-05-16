using CatalogService.Application.Products;
using MediatR;

namespace CatalogService.Application.Products.UpdateProduct;

public sealed record UpdateProductCommand(string Id, string Name, decimal Price, string? Description = null) : IRequest<ProductDto?>;
