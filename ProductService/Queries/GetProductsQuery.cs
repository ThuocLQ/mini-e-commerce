using MediatR;
using ProductService.Models;

namespace ProductService;

public record GetProductsQuery() :  IRequest<List<Product>>;