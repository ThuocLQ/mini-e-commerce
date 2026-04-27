using CatalogService.Models;
using MediatR;

namespace CatalogService.Queries;

public record SearchProductsQuery(string? keyword) : IRequest<List<Product?>>;