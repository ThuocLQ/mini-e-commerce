using CatalogService.Application.Products;
using MediatR;

namespace CatalogService.Application.Products.GetProductsByPrice;

public record GetProductsByPriceQuery(decimal Min, decimal Max) : IRequest<List<ProductDto>>;
