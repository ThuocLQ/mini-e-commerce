using CatalogService.Models;
using MediatR;

namespace CatalogService;

public record GetProductsQuery() :  IRequest<List<Product>>;