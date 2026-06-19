using System.Data;
using OrderingService.Domain.OrderPaymentSagas;

namespace OrderingService.Application.Abstractions;

public interface IOrderPaymentSagaRepository
{
    Task<OrderPaymentSaga?> GetByOrderIdAsync(
        Guid orderId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        OrderPaymentSaga saga,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
