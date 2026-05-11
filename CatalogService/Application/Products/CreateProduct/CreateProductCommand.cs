using CatalogService.Application.Products;
using MediatR;

namespace CatalogService.Application.Products.CreateProduct;

public record CreateProductCommand(string Name, decimal Price) : IRequest<ProductDto>;
