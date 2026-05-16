using MediatR;

namespace CatalogService.Application.Products.DeleteProduct;

public sealed record DeleteProductCommand(string Id) : IRequest<bool>;
