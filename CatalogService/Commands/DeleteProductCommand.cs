using MediatR;

namespace CatalogService.Commands;

public record DeleteProductCommand(string Id) : IRequest<bool>;