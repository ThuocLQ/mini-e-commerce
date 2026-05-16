using CatalogService.Application.Abstractions;
using Dapper;
using System.Data;
using CatalogService.Domain.Products;

namespace CatalogService.Infrastructure.Persistence;

public sealed class DapperProductRepository : IProductRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperProductRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Product> CreateAsync(Product product, CancellationToken cancellationToken = default)
    {
        using var connection = CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO Products (Id, Name, Description, Price)
            VALUES (@Id, @Name, @Description, @Price)
            """, new
        {
            product.Id,
            product.Name,
            product.Description,
            product.Price
        }, cancellationToken: cancellationToken));

        return product;
    }

    public async Task<Product?> UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        using var connection = CreateConnection();

        var affectedRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Products
            SET Name = @Name, Description = @Description, Price = @Price
            WHERE Id = @Id;
            """, new
        {
            product.Id,
            product.Name,
            product.Description,
            product.Price
        }, cancellationToken: cancellationToken));

        if (affectedRows == 0)
            return null;

        return await GetByIdAsync(product.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        using var connection = CreateConnection();

        var affectedRows = await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM Products
            WHERE Id = @Id;
            """, new { Id = id }, cancellationToken: cancellationToken));

        return affectedRows > 0;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = CreateConnection();

        var rows = await connection.QueryAsync<ProductRow>(new CommandDefinition("""
            SELECT Id, Name, Description, Price
            FROM Products
            ORDER BY Name;
            """, cancellationToken: cancellationToken));

        return rows.Select(ToDomain).ToList();
    }

    public async Task<Product?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        using var connection = CreateConnection();

        var row = await connection.QueryFirstOrDefaultAsync<ProductRow>(new CommandDefinition("""
            SELECT Id, Name, Description, Price
            FROM Products
            WHERE Id = @Id;
            """, new { Id = id }, cancellationToken: cancellationToken));

        return row is null ? null : ToDomain(row);
    }

    public async Task<List<Product>> SearchAsync(string? keyword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return (await GetAllAsync(cancellationToken)).ToList();

        using var connection = CreateConnection();

        var rows = await connection.QueryAsync<ProductRow>(new CommandDefinition("""
            SELECT Id, Name, Description, Price
            FROM Products
            WHERE LOWER(Name) LIKE LOWER(@Keyword)
            ORDER BY Name;
            """, new { Keyword = $"%{keyword}%" }, cancellationToken: cancellationToken));

        return rows.Select(ToDomain).ToList();
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        using var connection = CreateConnection();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM Products
            """, cancellationToken: cancellationToken));
    }

    public async Task<List<Product>> GetByPriceRangeAsync(decimal min, decimal max, CancellationToken cancellationToken = default)
    {
        using var connection = CreateConnection();

        var rows = await connection.QueryAsync<ProductRow>(new CommandDefinition("""
            SELECT Id, Name, Description, Price
            FROM Products
            WHERE Price BETWEEN @Min AND @Max
            ORDER BY Price;
            """, new { Min = min, Max = max }, cancellationToken: cancellationToken));

        return rows.Select(ToDomain).ToList();
    }

    private IDbConnection CreateConnection()
    {
        return _connectionFactory.CreateConnection();
    }

    private static Product ToDomain(ProductRow row)
    {
        return new Product(row.Id, row.Name, row.Description, Convert.ToDecimal(row.Price));
    }

    private sealed record ProductRow(string Id, string Name, string Description, double Price);
}
