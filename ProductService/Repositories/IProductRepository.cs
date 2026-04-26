using ProductService.Models;

namespace ProductService.Repositories;

public interface IProductRepository
{
    Task<Product> CreateAsync(string name, decimal price);
    Task<Product?> UpdateAsync(string id, string name, decimal price);
    Task<bool> DeleteAsync(string id);
    
    Task<List<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(string id);
    Task<List<Product>> SearchAsync(string? keyword);
    Task<int> CountAsync();
    Task<List<Product>> GetByPriceRangeAsync(decimal min, decimal Max);
}