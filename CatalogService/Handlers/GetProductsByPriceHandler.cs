using CatalogService.Models;
using CatalogService.Queries;
using CatalogService.Repositories;
using MediatR;

namespace CatalogService.Handlers;

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