using CatalogService.Models;
using MediatR;

namespace CatalogService;

public record GetProductByIdQuery(string Id) :  IRequest<Product?>;