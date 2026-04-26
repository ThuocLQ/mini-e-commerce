using Dapper;
using Microsoft.Data.Sqlite;
using ProductService.Models;

namespace ProductService.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly string _connectionString;
    
    public ProductRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
                            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }
    
    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }
    
    public async Task<Product> CreateAsync(string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name) || price <= 0)
            throw new ArgumentException("Invalid product name or price.");
        
        var product = new Product
        {   
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Price = price
        };
        
        await using var connection = CreateConnection();
        
        await connection.ExecuteAsync("""
            INSERT INTO Products (Id, Name, Price)
            VALUES (@Id, @Name, @Price)
            """, product);
        
            return product;
    }

    public async Task<Product?> UpdateAsync(string id, string name,  decimal price)
    {
        if (string.IsNullOrWhiteSpace(name) || price <= 0)
            throw new ArgumentException("Invalid product name or price.");
        
        await using var connection = CreateConnection();
        
        var affectedRows = await connection.ExecuteAsync("""
            UPDATE Products 
            SET Name = @Name, Price = @Price
            WHERE Id = @Id;
            """, new { Id = id, Name = name, Price = price });
        
        if (affectedRows == 0)
            return null;
        
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(string id)
    {
       await using var connection = CreateConnection();
        
        var affectedRows = await connection.ExecuteAsync("""
            DELETE FROM Products 
            WHERE Id = @Id;
            """, new { Id = id });
        
        return affectedRows > 0;
    }

    public async Task<List<Product>> GetAllAsync()
    {
        await using var connection = CreateConnection();
        
        var products = await connection.QueryAsync<Product>("""
            SELECT Id, Name, Price 
            FROM Products 
            ORDER BY Name;
            """);
        
        return products.ToList();
    }

    public async Task<Product?> GetByIdAsync(string id)
    {
        await using var connection = CreateConnection();
        
        return await connection.QueryFirstOrDefaultAsync<Product>("""
            SELECT Id, Name, Price 
            FROM Products 
            WHERE Id = @Id;
            """, new { Id = id });
    }

    public async Task<List<Product>> SearchAsync(string? keyword)
    {
        await using var connection = CreateConnection();
        
        if (string.IsNullOrWhiteSpace(keyword))
            return await GetAllAsync();
        
        var products = await connection.QueryAsync<Product>("""
            SELECT Id, Name, Price 
            FROM Products 
            WHERE LOWER(Name) LIKE '%' || LOWER(@Keyword) LIKE '%' 
            ORDER BY Name;
            """, new { Keyword = $"%{keyword}%" });
        
        return products.ToList();
    }

    public async Task<int> CountAsync()
    {
        await using var connection = CreateConnection();
        return await connection.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM Products
            """);
        
    }

    public async Task<List<Product>> GetByPriceRangeAsync(decimal min, decimal Max)
    {
       await using var connection = CreateConnection();
        
        var products = await connection.QueryAsync<Product>("""
            SELECT Id, Name, Price 
            FROM Products 
            WHERE Price BETWEEN @Min AND @Max
            ORDER BY Price;
            """, new { Min = min, Max = Max });
        
        return products.ToList();
    }

}