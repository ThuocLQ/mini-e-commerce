using CatalogService.Application.Products;
using MediatR;

namespace CatalogService.Application.Products.SearchProducts;

public record SearchProductsQuery(string? Keyword) : IRequest<List<ProductDto>>;
