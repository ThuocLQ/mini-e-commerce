using System.Data;
using CatalogService.Application.Abstractions;
using Dapper;

namespace CatalogService.Infrastructure.Persistence;

public sealed class DapperInventoryReservationRepository : IInventoryReservationRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperInventoryReservationRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<InventoryReservationResult> ReserveAsync(Guid orderId, IReadOnlyList<InventoryReservationItem> items, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var existingStatus = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT Status FROM InventoryReservations WHERE OrderId = @OrderId FOR UPDATE", new { OrderId = orderId }, transaction, cancellationToken: cancellationToken));
        if (existingStatus is not null)
        {
            transaction.Commit();
            return existingStatus == "Reserved"
                ? new InventoryReservationResult(true)
                : new InventoryReservationResult(false, "Inventory reservation is no longer active.");
        }

        var productIds = items.Select(item => item.ProductId).ToArray();
        var products = (await connection.QueryAsync<StockRow>(new CommandDefinition("""
            SELECT Id, StockQuantity, ReservedQuantity
            FROM Products
            WHERE Id = ANY(@ProductIds)
            ORDER BY Id
            FOR UPDATE
            """, new { ProductIds = productIds }, transaction, cancellationToken: cancellationToken))).ToDictionary(row => row.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                transaction.Rollback();
                return new InventoryReservationResult(false, $"Product '{item.ProductId}' was not found.");
            }

            if (product.StockQuantity - product.ReservedQuantity < item.Quantity)
            {
                transaction.Rollback();
                return new InventoryReservationResult(false, $"Insufficient inventory for product '{item.ProductId}'.");
            }
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO InventoryReservations (OrderId, Status, ExpiresAtUtc, CreatedAtUtc, UpdatedAtUtc)
            VALUES (@OrderId, 'Reserved', @ExpiresAtUtc, @Now, @Now)
            """, new { OrderId = orderId, ExpiresAtUtc = expiresAtUtc, Now = DateTime.UtcNow }, transaction, cancellationToken: cancellationToken));

        foreach (var item in items)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO InventoryReservationItems (OrderId, ProductId, Quantity)
                VALUES (@OrderId, @ProductId, @Quantity);
                UPDATE Products SET ReservedQuantity = ReservedQuantity + @Quantity WHERE Id = @ProductId;
                """, new { OrderId = orderId, item.ProductId, item.Quantity }, transaction, cancellationToken: cancellationToken));
        }

        transaction.Commit();
        return new InventoryReservationResult(true);
    }

    public Task ReleaseAsync(Guid orderId, CancellationToken cancellationToken = default) => ChangeReservationAsync(orderId, "Released", false, cancellationToken);
    public Task CommitAsync(Guid orderId, CancellationToken cancellationToken = default) => ChangeReservationAsync(orderId, "Committed", true, cancellationToken);

    public async Task<int> ReleaseExpiredAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var orderIds = (await connection.QueryAsync<Guid>(new CommandDefinition("""
            SELECT OrderId FROM InventoryReservations
            WHERE Status = 'Reserved' AND ExpiresAtUtc <= @Now
            ORDER BY ExpiresAtUtc
            LIMIT 100
            """, new { Now = DateTime.UtcNow }, cancellationToken: cancellationToken))).ToList();

        foreach (var orderId in orderIds)
        {
            await ReleaseAsync(orderId, cancellationToken);
        }

        return orderIds.Count;
    }

    private async Task ChangeReservationAsync(Guid orderId, string targetStatus, bool deductStock, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var status = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT Status FROM InventoryReservations WHERE OrderId = @OrderId FOR UPDATE", new { OrderId = orderId }, transaction, cancellationToken: cancellationToken));
        if (status != "Reserved") { transaction.Commit(); return; }

        var items = (await connection.QueryAsync<InventoryReservationItem>(new CommandDefinition(
            "SELECT ProductId, Quantity FROM InventoryReservationItems WHERE OrderId = @OrderId", new { OrderId = orderId }, transaction, cancellationToken: cancellationToken))).ToList();
        foreach (var item in items)
        {
            await connection.ExecuteAsync(new CommandDefinition(deductStock
                ? "UPDATE Products SET StockQuantity = StockQuantity - @Quantity, ReservedQuantity = ReservedQuantity - @Quantity WHERE Id = @ProductId"
                : "UPDATE Products SET ReservedQuantity = ReservedQuantity - @Quantity WHERE Id = @ProductId",
                new { item.ProductId, item.Quantity }, transaction, cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE InventoryReservations SET Status = @TargetStatus, UpdatedAtUtc = @Now WHERE OrderId = @OrderId",
            new { OrderId = orderId, TargetStatus = targetStatus, Now = DateTime.UtcNow }, transaction, cancellationToken: cancellationToken));
        transaction.Commit();
    }

    private sealed record StockRow(string Id, int StockQuantity, int ReservedQuantity);
}
