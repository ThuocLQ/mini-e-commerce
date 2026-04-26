using MediatR;

namespace ProductService.Commands;

public record DeleteProductCommand(string id) : IRequest<bool>;