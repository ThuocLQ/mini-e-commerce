using CatalogService.Application.Products;
using MediatR;

namespace CatalogService.Application.Products.GetProductById;

public record GetProductByIdQuery(string Id) : IRequest<ProductDto?>;
