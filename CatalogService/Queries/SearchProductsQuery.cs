using CatalogService.Models;
using MediatR;

namespace CatalogService.Queries;

public record SearchProductsQuery(string? Keyword) : IRequest<List<Product>>;
