using CatalogService.Application.Products;
using MediatR;

namespace CatalogService.Application.Products.UpdateProduct;

public record UpdateProductCommand(string Id, string Name, decimal Price) : IRequest<ProductDto?>;
