using MediatR;
using ProductService.Models;

namespace ProductService.Queries;

public record GetProductsByPriceQuery(decimal min, decimal max) : IRequest<List<Product?>>;