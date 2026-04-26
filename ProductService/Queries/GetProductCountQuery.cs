using MediatR;

namespace ProductService.Queries;

public record GetProductCountQuery() : IRequest<int>;