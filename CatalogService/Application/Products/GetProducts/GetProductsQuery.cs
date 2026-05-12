using CatalogService.Application.Products;
using MediatR;

namespace CatalogService.Application.Products.GetProducts;

public sealed record GetProductsQuery() : IRequest<IReadOnlyList<ProductDto>>;
