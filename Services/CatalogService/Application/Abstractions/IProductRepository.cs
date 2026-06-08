
using CatalogService.Application.Products;
using CatalogService.Domain.Products;

namespace CatalogService.Application.Abstractions;

public interface IProductRepository
{
    Task<Product> CreateAsync(Product product, CancellationToken cancellationToken = default);
    Task<Product?> UpdateAsync(Product product, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Product?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<List<Product>> SearchAsync(ProductQueryCriteria criteria, CancellationToken cancellationToken = default);
    Task<List<Product>> SearchAsync(string? keyword, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<List<Product>> GetByPriceRangeAsync(decimal min, decimal max, CancellationToken cancellationToken = default);
}
