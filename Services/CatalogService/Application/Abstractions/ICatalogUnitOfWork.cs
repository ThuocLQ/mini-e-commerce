using System.Data;

namespace CatalogService.Application.Abstractions;

public interface ICatalogUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<IDbTransaction, Task<T>> operation, CancellationToken cancellationToken = default);
}
