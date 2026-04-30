using Grpc.Core;
using CatalogService.Grpc;
using CatalogService.Repositories;

namespace CatalogService.GrpcServices;

public class CatalogGrpcService : CatalogGrpc.CatalogGrpcBase
{
    private readonly IProductRepository  _productRepository;
    
    public CatalogGrpcService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }
    
    public override async Task<ProductResponse> GetProductById(GetProductByIdRequest request, ServerCallContext context)
    {
        var product = await _productRepository.GetByIdAsync(request.Id);
        
        if (product == null)
        {
            return new ProductResponse
            {
                Found = false
            };
        }
        
        return new ProductResponse()
        {
            Id = product.Id,
            Name = product.Name,
            Price = (double)product.Price,
            Found = true,
            Description = "Success"
        };
    }
}