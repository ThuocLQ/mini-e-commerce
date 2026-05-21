using Dapper;
using Npgsql;
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
            """, new { Id = id }, cancellationToken: cancellationToken));

        if (orderRow is null)
        {
            return null;
        }

        var itemRows = await connection.QueryAsync<OrderItemRow>(new CommandDefinition("""
            SELECT Id, OrderId, ProductId, ProductName, UnitPrice, Quantity
            FROM OrderItems
            WHERE OrderId = @OrderId;
            """, new { OrderId = id }, cancellationToken: cancellationToken));

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
            CustomerId = customerId,
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
                order.Id,
                order.CustomerId,
                order.CreatedAtUtc,
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
                    item.Id,
                    OrderId = order.Id,
                    item.ProductId,
                    item.ProductName,
                    item.UnitPrice,
                    item.Quantity,
                    item.TotalPrice
                }, transaction, cancellationToken: cancellationToken));
            }

            transaction.Commit();

            return order;
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UniqueViolation &&
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
            row.Id,
            row.CustomerId,
            row.CreatedAtUtc,
            Enum.Parse<OrderStatus>(row.Status),
            row.IdempotencyKey);

        foreach (var itemRow in itemRows)
        {
            order.AddItem(new OrderItem(
                itemRow.Id,
                itemRow.ProductId,
                itemRow.ProductName,
                itemRow.UnitPrice,
                itemRow.Quantity));
        }

        return order;
    }

    private sealed record OrderRow(
        Guid Id,
        Guid CustomerId,
        DateTime CreatedAtUtc,
        string Status,
        string? IdempotencyKey);

    private sealed record OrderItemRow(
        Guid Id,
        Guid OrderId,
        Guid ProductId,
        string ProductName,
        decimal UnitPrice,
        int Quantity);
}
