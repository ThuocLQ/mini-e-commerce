using CatalogService.Models;

namespace CatalogService.Application.Abstractions;

public interface IProductRepository
{
    Task<Product> CreateAsync(string name, decimal price, CancellationToken cancellationToken = default);
    Task<Product?> UpdateAsync(string id, string name, decimal price, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Product?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<List<Product>> SearchAsync(string? keyword, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<List<Product>> GetByPriceRangeAsync(decimal min, decimal max, CancellationToken cancellationToken = default);
}