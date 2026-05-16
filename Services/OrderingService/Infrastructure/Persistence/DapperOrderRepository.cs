using Dapper;
using Microsoft.Data.Sqlite;
using OrderingService.Application.Abstractions;
using OrderingService.Domain.Orders;

namespace OrderingService.Infrastructure.Persistence;

public sealed class DapperOrderRepository : IOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperOrderRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var orderRows = (await connection.QueryAsync<OrderRow>(new CommandDefinition("""
            SELECT Id, CustomerId, CreatedAtUtc, Status, IdempotencyKey
            FROM Orders
            ORDER BY CreatedAtUtc DESC;
            """, cancellationToken: cancellationToken))).ToList();

        var itemRows = (await connection.QueryAsync<OrderItemRow>(new CommandDefinition("""
            SELECT Id, OrderId, ProductId, ProductName, UnitPrice, Quantity
            FROM OrderItems;
            """, cancellationToken: cancellationToken))).ToList();

        return MapOrders(orderRows, itemRows);
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var orderRow = await connection.QuerySingleOrDefaultAsync<OrderRow>(new CommandDefinition("""
            SELECT Id, CustomerId, CreatedAtUtc, Status, IdempotencyKey
            FROM Orders
            WHERE Id = @Id;
            """, new { Id = id.ToString() }, cancellationToken: cancellationToken));

        if (orderRow is null)
        {
            return null;
        }

        var itemRows = await connection.QueryAsync<OrderItemRow>(new CommandDefinition("""
            SELECT Id, OrderId, ProductId, ProductName, UnitPrice, Quantity
            FROM OrderItems
            WHERE OrderId = @OrderId;
            """, new { OrderId = id.ToString() }, cancellationToken: cancellationToken));

        return MapOrder(orderRow, itemRows);
    }

    public async Task<Order?> GetByCustomerAndIdempotencyKeyAsync(
        Guid customerId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var orderRow = await connection.QuerySingleOrDefaultAsync<OrderRow>(new CommandDefinition("""
            SELECT Id, CustomerId, CreatedAtUtc, Status, IdempotencyKey
            FROM Orders
            WHERE CustomerId = @CustomerId
              AND IdempotencyKey = @IdempotencyKey;
            """, new
        {
            CustomerId = customerId.ToString(),
            IdempotencyKey = idempotencyKey
        }, cancellationToken: cancellationToken));

        if (orderRow is null)
        {
            return null;
        }

        var itemRows = await connection.QueryAsync<OrderItemRow>(new CommandDefinition("""
            SELECT Id, OrderId, ProductId, ProductName, UnitPrice, Quantity
            FROM OrderItems
            WHERE OrderId = @OrderId;
            """, new { OrderId = orderRow.Id }, cancellationToken: cancellationToken));

        return MapOrder(orderRow, itemRows);
    }

    public async Task<Order> CreateAsync(Order order, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        try
        {
            using var transaction = connection.BeginTransaction();

            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO Orders (Id, CustomerId, CreatedAtUtc, Status, TotalAmount, IdempotencyKey)
                VALUES (@Id, @CustomerId, @CreatedAtUtc, @Status, @TotalAmount, @IdempotencyKey);
                """, new
            {
                Id = order.Id.ToString(),
                CustomerId = order.CustomerId.ToString(),
                CreatedAtUtc = order.CreatedAtUtc.ToString("O"),
                Status = order.Status.ToString(),
                order.TotalAmount,
                order.IdempotencyKey
            }, transaction, cancellationToken: cancellationToken));

            foreach (var item in order.Items)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO OrderItems (Id, OrderId, ProductId, ProductName, UnitPrice, Quantity, TotalPrice)
                    VALUES (@Id, @OrderId, @ProductId, @ProductName, @UnitPrice, @Quantity, @TotalPrice);
                    """, new
                {
                    Id = item.Id.ToString(),
                    OrderId = order.Id.ToString(),
                    ProductId = item.ProductId.ToString(),
                    item.ProductName,
                    item.UnitPrice,
                    item.Quantity,
                    item.TotalPrice
                }, transaction, cancellationToken: cancellationToken));
            }

            transaction.Commit();

            return order;
        }
        catch (SqliteException ex) when (
            ex.SqliteErrorCode == 19 &&
            order.IdempotencyKey is not null)
        {
            var existingOrder = await GetByCustomerAndIdempotencyKeyAsync(
                order.CustomerId,
                order.IdempotencyKey,
                cancellationToken);

            if (existingOrder is not null)
            {
                return existingOrder;
            }

            throw;
        }
    }

    private static IReadOnlyList<Order> MapOrders(
        IReadOnlyList<OrderRow> orderRows,
        IReadOnlyList<OrderItemRow> itemRows)
    {
        return orderRows
            .Select(orderRow => MapOrder(
                orderRow,
                itemRows.Where(itemRow => itemRow.OrderId == orderRow.Id)))
            .ToList();
    }

    private static Order MapOrder(OrderRow row, IEnumerable<OrderItemRow> itemRows)
    {
        var order = new Order(
            Guid.Parse(row.Id),
            Guid.Parse(row.CustomerId),
            DateTime.Parse(row.CreatedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
            Enum.Parse<OrderStatus>(row.Status),
            row.IdempotencyKey);

        foreach (var itemRow in itemRows)
        {
            order.AddItem(new OrderItem(
                Guid.Parse(itemRow.Id),
                Guid.Parse(itemRow.ProductId),
                itemRow.ProductName,
                Convert.ToDecimal(itemRow.UnitPrice),
                checked((int)itemRow.Quantity)));
        }

        return order;
    }

    private sealed record OrderRow(
        string Id,
        string CustomerId,
        string CreatedAtUtc,
        string Status,
        string? IdempotencyKey);

    private sealed record OrderItemRow(
        string Id,
        string OrderId,
        string ProductId,
        string ProductName,
        double UnitPrice,
        long Quantity);
}
