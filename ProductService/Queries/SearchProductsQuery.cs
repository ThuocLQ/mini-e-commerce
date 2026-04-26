using MediatR;
using ProductService.Models;

namespace ProductService.Queries;

public record SearchProductsQuery(string? keyword) : IRequest<List<Product?>>;