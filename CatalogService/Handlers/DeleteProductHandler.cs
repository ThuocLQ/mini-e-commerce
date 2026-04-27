using CatalogService.Commands;
using CatalogService.Repositories;
using MediatR;
using CatalogService.Models;

namespace CatalogService.Handlers;

public class DeleteProductHandler : IRequestHandler<DeleteProductCommand, bool>
{
    private readonly IProductRepository _productRepository ;
    
    public DeleteProductHandler(IProductRepository productRepository)
    {
      _productRepository = productRepository;
    }
    
    public async Task<bool> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
       return await _productRepository.DeleteAsync(request.id);
    }
}