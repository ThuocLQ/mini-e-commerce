using CatalogService.Models;
using MediatR;

namespace CatalogService.Queries;

public record GetProductsByPriceQuery(decimal min, decimal max) : IRequest<List<Product?>>;