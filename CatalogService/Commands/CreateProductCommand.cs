using CatalogService.Models;
using MediatR;

namespace CatalogService.Commands;

public record CreateProductCommand(string Name, decimal Price) :  IRequest<Product>;