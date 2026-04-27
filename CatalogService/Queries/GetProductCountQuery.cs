using MediatR;

namespace CatalogService.Queries;

public record GetProductCountQuery() : IRequest<int>;