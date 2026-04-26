using MediatR;
using ProductService.Models;
using ProductService.Repositories;

namespace ProductService.Handlers;

public class GetProductsHandler : IRequestHandler<GetProductsQuery, List<Product>>
{
    private readonly IProductRepository _productRepository;

    public GetProductsHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<List<Product>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        return await _productRepository.GetAllAsync();
    }
}