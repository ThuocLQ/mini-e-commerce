using CatalogService.Application.Products;
using MediatR;

namespace CatalogService.Application.Products.GetProductById;

public sealed record GetProductByIdQuery(string Id) : IRequest<ProductDto?>;
