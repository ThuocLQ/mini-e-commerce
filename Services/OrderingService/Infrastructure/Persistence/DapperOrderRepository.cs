using Dapper;
using Npgsql;
using OrderingService.Application.Abstractions;
using OrderingService.Application.Orders;
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
            SELECT Id, CustomerId, CreatedAtUtc, Status, IdempotencyKey, CheckoutRequestHash, CheckoutBasketId, CheckoutBasketVersion, Currency, DiscountCode, DiscountAmount
            FROM Orders
            ORDER BY CreatedAtUtc DESC;
            """, cancellationToken: cancellationToken))).ToList();

        var itemRows = (await connection.QueryAsync<OrderItemRow>(new CommandDefinition("""
            SELECT Id, OrderId, ProductId, ProductName, UnitPrice, Quantity
            FROM OrderItems;
            """, cancellationToken: cancellationToken))).ToList();

        return MapOrders(orderRows, itemRows);
    }

    public async Task<IReadOnlyList<Order>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var orderRows = (await connection.QueryAsync<OrderRow>(new CommandDefinition("""
            SELECT Id, CustomerId, CreatedAtUtc, Status, IdempotencyKey, CheckoutRequestHash, CheckoutBasketId, CheckoutBasketVersion, Currency, DiscountCode, DiscountAmount
            FROM Orders
            WHERE CustomerId = @CustomerId
            ORDER BY CreatedAtUtc DESC;
            """, new { CustomerId = customerId }, cancellationToken: cancellationToken))).ToList();

        if (orderRows.Count == 0)
        {
            return [];
        }

        var itemRows = (await connection.QueryAsync<OrderItemRow>(new CommandDefinition("""
            SELECT Id, OrderId, ProductId, ProductName, UnitPrice, Quantity
            FROM OrderItems
            WHERE OrderId = ANY(@OrderIds);
            """, new { OrderIds = orderRows.Select(order => order.Id).ToArray() }, cancellationToken: cancellationToken))).ToList();

        return MapOrders(orderRows, itemRows);
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await GetByIdAsync(connection, null, id, cancellationToken);
    }

    public async Task<Order?> GetByIdAsync(
        Guid id,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return await GetByIdAsync(transaction.Connection!, transaction, id, cancellationToken);
    }

    private static async Task<Order?> GetByIdAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction? transaction,
        Guid id,
        CancellationToken cancellationToken)
    {
        var sql = transaction is null
            ? """
              SELECT Id, CustomerId, CreatedAtUtc, Status, IdempotencyKey, CheckoutRequestHash, CheckoutBasketId, CheckoutBasketVersion, Currency, DiscountCode, DiscountAmount
              FROM Orders
              WHERE Id = @Id;
              """
            : """
              SELECT Id, CustomerId, CreatedAtUtc, Status, IdempotencyKey, CheckoutRequestHash, CheckoutBasketId, CheckoutBasketVersion, Currency, DiscountCode, DiscountAmount
              FROM Orders
              WHERE Id = @Id
              FOR UPDATE;
              """;

        var orderRow = await connection.QuerySingleOrDefaultAsync<OrderRow>(new CommandDefinition(
            sql,
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken));

        if (orderRow is null)
        {
            return null;
        }

        var itemRows = await connection.QueryAsync<OrderItemRow>(new CommandDefinition("""
            SELECT Id, OrderId, ProductId, ProductName, UnitPrice, Quantity
            FROM OrderItems
            WHERE OrderId = @OrderId;
            """, new { OrderId = id }, transaction, cancellationToken: cancellationToken));

        return MapOrder(orderRow, itemRows);
    }

    public async Task<Order?> GetByCustomerAndIdempotencyKeyAsync(
        Guid customerId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var orderRow = await connection.QuerySingleOrDefaultAsync<OrderRow>(new CommandDefinition("""
            SELECT Id, CustomerId, CreatedAtUtc, Status, IdempotencyKey, CheckoutRequestHash, CheckoutBasketId, CheckoutBasketVersion, Currency, DiscountCode, DiscountAmount
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

    public async Task<Order?> GetByCustomerAndCheckoutBasketAsync(
        Guid customerId,
        Guid basketId,
        long basketVersion,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var orderRow = await connection.QuerySingleOrDefaultAsync<OrderRow>(new CommandDefinition("""
            SELECT Id, CustomerId, CreatedAtUtc, Status, IdempotencyKey, CheckoutRequestHash, CheckoutBasketId, CheckoutBasketVersion, Currency, DiscountCode, DiscountAmount
            FROM Orders
            WHERE CustomerId = @CustomerId
              AND CheckoutBasketId = @BasketId
              AND CheckoutBasketVersion = @BasketVersion;
            """, new
        {
            CustomerId = customerId,
            BasketId = basketId,
            BasketVersion = basketVersion
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

    public async Task<Order> CreateAsync(
        Order order,
        System.Data.IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        if (transaction is not null)
        {
            try
            {
                await InsertAsync(transaction.Connection!, order, transaction, cancellationToken);
                return order;
            }
            catch (PostgresException ex) when (
                ex.SqlState == PostgresErrorCodes.UniqueViolation &&
                order.IdempotencyKey is not null)
            {
                throw new OrderAlreadyExistsException(order.CustomerId, order.IdempotencyKey);
            }
        }

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        try
        {
            using var ownedTransaction = connection.BeginTransaction();
            await InsertAsync(connection, order, ownedTransaction, cancellationToken);
            ownedTransaction.Commit();

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

    public async Task<bool> TryUpdateStatusAsync(
        Guid orderId,
        OrderStatus newStatus,
        IReadOnlyCollection<OrderStatus> expectedCurrentStatuses,
        System.Data.IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        if (expectedCurrentStatuses.Count == 0)
        {
            throw new ArgumentException("At least one expected status is required.", nameof(expectedCurrentStatuses));
        }

        var parameters = new
        {
            Id = orderId,
            Status = newStatus.ToString(),
            ExpectedStatuses = expectedCurrentStatuses.Select(status => status.ToString()).ToArray()
        };

        if (transaction is not null)
        {
            var affectedRows = await transaction.Connection!.ExecuteAsync(new CommandDefinition("""
                UPDATE Orders
                SET Status = @Status
                WHERE Id = @Id
                  AND Status = ANY(@ExpectedStatuses);
                """, parameters, transaction, cancellationToken: cancellationToken));

            return affectedRows == 1;
        }

        using var connection = _connectionFactory.CreateConnection();
        var updatedRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Orders
            SET Status = @Status
            WHERE Id = @Id
              AND Status = ANY(@ExpectedStatuses);
            """, parameters, cancellationToken: cancellationToken));

        return updatedRows == 1;
    }

    private static async Task InsertAsync(
        System.Data.IDbConnection connection,
        Order order,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO Orders (Id, CustomerId, CreatedAtUtc, Status, TotalAmount, Currency, DiscountCode, DiscountAmount, IdempotencyKey, CheckoutRequestHash, CheckoutBasketId, CheckoutBasketVersion)
            VALUES (@Id, @CustomerId, @CreatedAtUtc, @Status, @TotalAmount, @Currency, @DiscountCode, @DiscountAmount, @IdempotencyKey, @CheckoutRequestHash, @CheckoutBasketId, @CheckoutBasketVersion);
            """, new
        {
            order.Id,
            order.CustomerId,
            order.CreatedAtUtc,
            Status = order.Status.ToString(),
            order.TotalAmount,
            order.Currency,
            order.DiscountCode,
            order.DiscountAmount,
            order.IdempotencyKey,
            order.CheckoutRequestHash,
            order.CheckoutBasketId,
            order.CheckoutBasketVersion
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
            row.IdempotencyKey,
            row.Currency,
            row.CheckoutRequestHash,
            row.CheckoutBasketVersion,
            row.CheckoutBasketId);

        foreach (var itemRow in itemRows)
        {
            order.AddItem(new OrderItem(
                itemRow.Id,
                itemRow.ProductId,
                itemRow.ProductName,
                itemRow.UnitPrice,
                itemRow.Quantity));
        }

        if (row.DiscountAmount > 0 && !string.IsNullOrWhiteSpace(row.DiscountCode))
        {
            order.ApplyDiscount(row.DiscountCode, row.DiscountAmount);
        }

        return order;
    }

    private sealed record OrderRow(
        Guid Id,
        Guid CustomerId,
        DateTime CreatedAtUtc,
        string Status,
        string? IdempotencyKey,
        string? CheckoutRequestHash,
        Guid? CheckoutBasketId,
        long? CheckoutBasketVersion,
        string Currency,
        string? DiscountCode,
        decimal DiscountAmount);

    private sealed record OrderItemRow(
        Guid Id,
        Guid OrderId,
        Guid ProductId,
        string ProductName,
        decimal UnitPrice,
        int Quantity);
}
