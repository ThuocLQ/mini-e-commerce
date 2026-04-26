using MediatR;
using ProductService.Models;

namespace ProductService.Commands;

public record CreateProductCommand(string Name) :  IRequest<Product>;