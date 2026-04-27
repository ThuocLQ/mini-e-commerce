using CatalogService.Models;
using MediatR;

namespace CatalogService;

public record GetProductByIdQuery(string id) :  IRequest<Product?>;