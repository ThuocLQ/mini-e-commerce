using CatalogService.Models;
using MediatR;

namespace CatalogService.Queries;

public record GetProductsByPriceQuery(decimal Min, decimal Max) : IRequest<List<Product>>;
