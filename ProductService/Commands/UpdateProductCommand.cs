using MediatR;
using ProductService.Models;

namespace ProductService.Commands;

public record UpdateProductCommand(string id, string name) : IRequest<Product?>;