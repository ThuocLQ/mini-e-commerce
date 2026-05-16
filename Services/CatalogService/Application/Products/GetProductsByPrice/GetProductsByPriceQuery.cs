using CatalogService.Application.Products;
using MediatR;

namespace CatalogService.Application.Products.GetProductsByPrice;

public sealed record GetProductsByPriceQuery(decimal Min, decimal Max) : IRequest<List<ProductDto>>;
