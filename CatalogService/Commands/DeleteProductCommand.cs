using MediatR;

namespace CatalogService.Commands;

public record DeleteProductCommand(string id) : IRequest<bool>;