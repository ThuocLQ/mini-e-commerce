using MediatR;
using ProductService.Models;
using ProductService.Queries;
using ProductService.Repositories;

namespace ProductService.Handlers;

public class GetProductsByPriceHandler : IRequestHandler<GetProductsByPriceQuery, List<Product?>>
{
    private readonly IProductRepository _productRepository;
    
    public GetProductsByPriceHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }
    
    public async Task<List<Product?>> Handle(GetProductsByPriceQuery request, CancellationToken cancellationToken)
    {
        return await _productRepository.GetByPriceRangeAsync(request.min, request.max);
    }
}