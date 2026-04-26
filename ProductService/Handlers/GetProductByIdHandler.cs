using MediatR;
using ProductService.Models;
using ProductService.Repositories;

namespace ProductService.Handlers;

public class GetProductByIdHandler : IRequestHandler<GetProductByIdQuery, Product?>
{
    private readonly IProductRepository _productRepository;
    
    public GetProductByIdHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }
     
    public async Task<Product?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        return await _productRepository.GetByIdAsync(request.id);
    }
}