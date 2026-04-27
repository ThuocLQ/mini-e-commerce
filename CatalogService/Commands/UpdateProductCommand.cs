using CatalogService.Models;
using MediatR;

namespace CatalogService.Commands;

public record UpdateProductCommand(string id, string name, decimal price) : IRequest<Product?>;