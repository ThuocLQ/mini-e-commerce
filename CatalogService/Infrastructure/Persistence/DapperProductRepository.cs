using CatalogService.Application.Abstractions;
using CatalogService.Models;
using Dapper;
using Microsoft.Data.Sqlite;

namespace CatalogService.Infrastructure.Persistence;

public class DapperProductRepository : IProductRepository
{
    private readonly IDbConnectionFactory  _connectionFactory;

    public DapperProductRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Product> CreateAsync(string name, decimal price, CancellationToken cancellationToken = default)
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
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO Products (Id, Name, Price)
            VALUES (@Id, @Name, @Price)
            """, product, cancellationToken: cancellationToken));

        return product;
    }

    public async Task<Product?> UpdateAsync(string id, string name, decimal price, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name) || price <= 0)
            throw new ArgumentException("Invalid product name or price.");

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var affectedRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Products
            SET Name = @Name, Price = @Price
            WHERE Id = @Id;
            """, new { Id = id, Name = name, Price = price }, cancellationToken: cancellationToken));

        if (affectedRows == 0)
            return null;

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var affectedRows = await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM Products
            WHERE Id = @Id;
            """, new { Id = id }, cancellationToken: cancellationToken));

        return affectedRows > 0;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var products = await connection.QueryAsync<Product>(new CommandDefinition("""
            SELECT Id, Name, Price
            FROM Products
            ORDER BY Name;
            """, cancellationToken: cancellationToken));

        return products.ToList();
    }

    public async Task<Product?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT Id, Name, Description, Price
                           FROM Products
                           WHERE Id = @Id
                           """;
        using var connection = _connectionFactory.CreateConnection();
        var row =  await connection.QueryFirstOrDefaultAsync<Product>(sql, new { Id = id });
        
        if (row is null)
            return null;
        
        return new Product()
        {
            Id = row.Id,
            Name = row.Name,
            Price = row.Price
        };
    }

    public async Task<List<Product>> SearchAsync(string? keyword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return (await GetAllAsync(cancellationToken)).ToList();

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var products = await connection.QueryAsync<Product>(new CommandDefinition("""
            SELECT Id, Name, Price
            FROM Products
            WHERE LOWER(Name) LIKE LOWER(@Keyword)
            ORDER BY Name;
            """, new { Keyword = $"%{keyword}%" }, cancellationToken: cancellationToken));

        return products.ToList();
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM Products
            """, cancellationToken: cancellationToken));
    }

    public async Task<List<Product>> GetByPriceRangeAsync(decimal min, decimal max, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var products = await connection.QueryAsync<Product>(new CommandDefinition("""
            SELECT Id, Name, Price
            FROM Products
            WHERE Price BETWEEN @Min AND @Max
            ORDER BY Price;
            """, new { Min = min, Max = max }, cancellationToken: cancellationToken));

        return products.ToList();
    }
    
    private sealed record ProductRow(
        string Id,
        string Name,
        string Description,
        decimal Price);
}
