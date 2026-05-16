using CatalogService.Application.Abstractions;
using CatalogService.Application.Products;
using MediatR;

namespace CatalogService.Application.Products.GetProductsByPrice;

public sealed class GetProductsByPriceHandler : IRequestHandler<GetProductsByPriceQuery, List<ProductDto>>
{
    private readonly IProductRepository _productRepository;
    
    public GetProductsByPriceHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }
    
    public async Task<List<ProductDto>> Handle(GetProductsByPriceQuery request, CancellationToken cancellationToken)
    {
        var products = await _productRepository.GetByPriceRangeAsync(request.Min, request.Max, cancellationToken);

        return products.Select(ProductMapper.ToDto).ToList();
    }
}
