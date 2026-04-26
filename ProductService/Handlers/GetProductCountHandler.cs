using MediatR;
using ProductService.Queries;
using ProductService.Repositories;

namespace ProductService.Handlers;

public class GetProductCountHandler : IRequestHandler<GetProductCountQuery, int>
{
    private readonly IProductRepository _productRepository;
    
    public GetProductCountHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }
    
    public async Task<int> Handle(GetProductCountQuery request, CancellationToken cancellationToken)
    {
        return await _productRepository.CountAsync();
    }
}