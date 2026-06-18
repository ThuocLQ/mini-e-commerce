using System.Data;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Abstractions;

public interface IOrderRepository
{
    Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Order?> GetByCustomerAndIdempotencyKeyAsync(
        Guid customerId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
    Task<Order> CreateAsync(
        Order order,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
    Task<bool> TryUpdateStatusAsync(
        Guid orderId,
        OrderStatus newStatus,
        IReadOnlyCollection<OrderStatus> expectedCurrentStatuses,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
}
