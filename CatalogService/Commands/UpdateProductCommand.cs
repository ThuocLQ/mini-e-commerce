using CatalogService.Models;
using MediatR;

namespace CatalogService.Commands;

public record UpdateProductCommand(string Id, string Name, decimal Price) : IRequest<Product?>;