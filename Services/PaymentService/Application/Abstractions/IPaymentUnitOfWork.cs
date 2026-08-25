using System.Data;

namespace PaymentService.Application.Abstractions;

public interface IPaymentUnitOfWork
{
    Task<T> ExecuteAsync<T>(
        Func<IDbTransaction, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
