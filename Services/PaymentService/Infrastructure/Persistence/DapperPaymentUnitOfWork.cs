using System.Data;
using PaymentService.Application.Abstractions;

namespace PaymentService.Infrastructure.Persistence;

public sealed class DapperPaymentUnitOfWork : IPaymentUnitOfWork
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperPaymentUnitOfWork(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<IDbTransaction, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
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
