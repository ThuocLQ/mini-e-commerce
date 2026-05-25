using System.Data;

namespace OrderingService.Application.Abstractions;

public interface IOrderingUnitOfWork
{
    Task<T> ExecuteAsync<T>(
        Func<IDbTransaction, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
