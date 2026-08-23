using System.Data;
using OrderingService.Domain.OrderPaymentSagas;

namespace OrderingService.Application.Abstractions;

public interface IOrderPaymentSagaRepository
{
    Task<IReadOnlyList<OrderPaymentSaga>> GetTimedOutAsync(
        DateTime nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<OrderPaymentSaga?> GetByOrderIdAsync(
        Guid orderId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        OrderPaymentSaga saga,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
