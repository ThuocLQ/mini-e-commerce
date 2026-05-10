using CatalogService.Application.Abstractions;
using CatalogService.Models;
using MediatR;

namespace CatalogService.Handlers;

public class GetProductsHandler : IRequestHandler<GetProductsQuery, IReadOnlyList<Product>>
{
    private readonly IProductRepository _productRepository;

    public GetProductsHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<IReadOnlyList<Product>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        return await _productRepository.GetAllAsync(cancellationToken);
    }
}
