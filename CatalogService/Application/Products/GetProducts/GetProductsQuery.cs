using CatalogService.Application.Products;
using MediatR;

namespace CatalogService.Application.Products.GetProducts;

public record GetProductsQuery() : IRequest<IReadOnlyList<ProductDto>>;
