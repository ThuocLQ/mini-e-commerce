using CatalogService.Application.Products;
using MediatR;

namespace CatalogService.Application.Products.SearchProducts;

public sealed record SearchProductsQuery(string? Keyword) : IRequest<List<ProductDto>>;
