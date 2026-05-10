using CatalogService.Application.Abstractions;
using CatalogService.Models;
using CatalogService.Queries;
using MediatR;

namespace CatalogService.Handlers;

public class GetProductsByPriceHandler : IRequestHandler<GetProductsByPriceQuery, List<Product>>
{
    private readonly IProductRepository _productRepository;
    
    public GetProductsByPriceHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }
    
    public async Task<List<Product>> Handle(GetProductsByPriceQuery request, CancellationToken cancellationToken)
    {
        return await _productRepository.GetByPriceRangeAsync(request.Min, request.Max, cancellationToken);
    }
}
