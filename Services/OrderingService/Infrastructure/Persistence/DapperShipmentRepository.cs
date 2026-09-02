using System.Data;
using Dapper;
using OrderingService.Application.Abstractions;
using OrderingService.Domain.Fulfillment;

namespace OrderingService.Infrastructure.Persistence;

public sealed class DapperShipmentRepository : IShipmentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperShipmentRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<Shipment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await GetByOrderIdAsync(connection, null, orderId, cancellationToken);
    }

    public Task<Shipment?> GetByOrderIdAsync(Guid orderId, IDbTransaction transaction, CancellationToken cancellationToken = default) =>
        GetByOrderIdAsync(transaction.Connection!, transaction, orderId, cancellationToken);

    public async Task<IReadOnlyList<Shipment>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ShipmentRow>(new CommandDefinition("""
            SELECT Id, OrderId, Status, Carrier, TrackingNumber, CreatedAtUtc, UpdatedAtUtc
            FROM Shipments
            ORDER BY UpdatedAtUtc DESC
            LIMIT @Limit;
            """, new { Limit = Math.Clamp(limit, 1, 500) }, cancellationToken: cancellationToken));
        return rows.Select(Map).ToList();
    }

    public Task CreateAsync(Shipment shipment, IDbTransaction transaction, CancellationToken cancellationToken = default) =>
        transaction.Connection!.ExecuteAsync(new CommandDefinition("""
            INSERT INTO Shipments (Id, OrderId, Status, Carrier, TrackingNumber, CreatedAtUtc, UpdatedAtUtc)
            VALUES (@Id, @OrderId, @Status, @Carrier, @TrackingNumber, @CreatedAtUtc, @UpdatedAtUtc);
            """, new { shipment.Id, shipment.OrderId, Status = shipment.Status.ToString(), shipment.Carrier, shipment.TrackingNumber, shipment.CreatedAtUtc, shipment.UpdatedAtUtc }, transaction, cancellationToken: cancellationToken));

    public async Task<bool> UpdateAsync(Shipment shipment, ShipmentStatus expectedStatus, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var rows = await transaction.Connection!.ExecuteAsync(new CommandDefinition("""
            UPDATE Shipments
            SET Status = @Status, Carrier = @Carrier, TrackingNumber = @TrackingNumber, UpdatedAtUtc = @UpdatedAtUtc
            WHERE Id = @Id AND Status = @ExpectedStatus;
            """, new { shipment.Id, Status = shipment.Status.ToString(), shipment.Carrier, shipment.TrackingNumber, shipment.UpdatedAtUtc, ExpectedStatus = expectedStatus.ToString() }, transaction, cancellationToken: cancellationToken));
        return rows == 1;
    }

    public Task AddHistoryAsync(ShipmentStatusHistory history, IDbTransaction transaction, CancellationToken cancellationToken = default) =>
        transaction.Connection!.ExecuteAsync(new CommandDefinition("""
            INSERT INTO ShipmentStatusHistory (Id, ShipmentId, PreviousStatus, CurrentStatus, ActorId, Reason, OccurredAtUtc)
            VALUES (@Id, @ShipmentId, @PreviousStatus, @CurrentStatus, @ActorId, @Reason, @OccurredAtUtc);
            """, new { history.Id, history.ShipmentId, PreviousStatus = history.PreviousStatus?.ToString(), CurrentStatus = history.CurrentStatus.ToString(), history.ActorId, history.Reason, history.OccurredAtUtc }, transaction, cancellationToken: cancellationToken));

    public async Task<IReadOnlyList<ShipmentStatusHistory>> GetHistoryAsync(Guid shipmentId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<HistoryRow>(new CommandDefinition("""
            SELECT Id, ShipmentId, PreviousStatus, CurrentStatus, ActorId, Reason, OccurredAtUtc
            FROM ShipmentStatusHistory
            WHERE ShipmentId = @ShipmentId
            ORDER BY OccurredAtUtc DESC, Id DESC;
            """, new { ShipmentId = shipmentId }, cancellationToken: cancellationToken));
        return rows.Select(row => new ShipmentStatusHistory(row.Id, row.ShipmentId, ParseNullable(row.PreviousStatus), Enum.Parse<ShipmentStatus>(row.CurrentStatus), row.ActorId, row.Reason, row.OccurredAtUtc)).ToList();
    }

    private static async Task<Shipment?> GetByOrderIdAsync(IDbConnection connection, IDbTransaction? transaction, Guid orderId, CancellationToken cancellationToken)
    {
        var suffix = transaction is null ? ";" : " FOR UPDATE;";
        var row = await connection.QuerySingleOrDefaultAsync<ShipmentRow>(new CommandDefinition("""
            SELECT Id, OrderId, Status, Carrier, TrackingNumber, CreatedAtUtc, UpdatedAtUtc
            FROM Shipments
            WHERE OrderId = @OrderId
            """ + suffix, new { OrderId = orderId }, transaction, cancellationToken: cancellationToken));
        return row is null ? null : Map(row);
    }

    private static Shipment Map(ShipmentRow row) => new(row.Id, row.OrderId, Enum.Parse<ShipmentStatus>(row.Status), row.CreatedAtUtc, row.UpdatedAtUtc, row.Carrier, row.TrackingNumber);
    private static ShipmentStatus? ParseNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : Enum.Parse<ShipmentStatus>(value);
    private sealed record ShipmentRow(Guid Id, Guid OrderId, string Status, string? Carrier, string? TrackingNumber, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
    private sealed record HistoryRow(Guid Id, Guid ShipmentId, string? PreviousStatus, string CurrentStatus, Guid ActorId, string Reason, DateTime OccurredAtUtc);
}