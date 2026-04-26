using MediatR;
using ProductService.Models;

namespace ProductService.Commands;

public record CreateProductCommand(string name, decimal price) :  IRequest<Product>;