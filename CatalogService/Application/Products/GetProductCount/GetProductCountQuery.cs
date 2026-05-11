using MediatR;

namespace CatalogService.Application.Products.GetProductCount;

public record GetProductCountQuery() : IRequest<int>;
