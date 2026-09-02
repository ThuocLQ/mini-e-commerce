using System.Data;
using OrderingService.Domain.Fulfillment;

namespace OrderingService.Application.Abstractions;

public interface IShipmentRepository
{
    Task<Shipment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<Shipment?> GetByOrderIdAsync(Guid orderId, IDbTransaction transaction, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Shipment>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task CreateAsync(Shipment shipment, IDbTransaction transaction, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Shipment shipment, ShipmentStatus expectedStatus, IDbTransaction transaction, CancellationToken cancellationToken = default);
    Task AddHistoryAsync(ShipmentStatusHistory history, IDbTransaction transaction, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShipmentStatusHistory>> GetHistoryAsync(Guid shipmentId, CancellationToken cancellationToken = default);
}