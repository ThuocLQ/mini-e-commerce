using MediatR;
using ProductService.Models;

namespace ProductService;

public record GetProductByIdQuery(string id) :  IRequest<Product?>;