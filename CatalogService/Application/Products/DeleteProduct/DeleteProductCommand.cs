using MediatR;

namespace CatalogService.Application.Products.DeleteProduct;

public record DeleteProductCommand(string Id) : IRequest<bool>;
