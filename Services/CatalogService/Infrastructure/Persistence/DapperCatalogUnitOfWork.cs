using System.Data;
using CatalogService.Application.Abstractions;

namespace CatalogService.Infrastructure.Persistence;

public sealed class DapperCatalogUnitOfWork(IDbConnectionFactory connectionFactory) : ICatalogUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(Func<IDbTransaction, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var result = await operation(transaction);
            transaction.Commit();
            return result;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
