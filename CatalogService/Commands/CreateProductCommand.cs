using CatalogService.Models;
using MediatR;

namespace CatalogService.Commands;

public record CreateProductCommand(string name, decimal price) :  IRequest<Product>;