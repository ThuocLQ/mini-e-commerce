using MediatR;

namespace CatalogService.Application.Products.GetProductCount;

public sealed record GetProductCountQuery() : IRequest<int>;
