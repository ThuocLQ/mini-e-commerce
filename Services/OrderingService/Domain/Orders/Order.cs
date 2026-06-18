namespace OrderingService.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    public Guid Id { get; }
    public Guid CustomerId { get; }
    public DateTime CreatedAtUtc { get; }
    public OrderStatus Status { get; private set; }
    public string? IdempotencyKey { get; }
    public IReadOnlyList<OrderItem> Items => _items;
    public decimal TotalAmount => _items.Sum(item => item.TotalPrice);

    public Order(Guid id, Guid customerId, DateTime createdAtUtc, OrderStatus status, string? idempotencyKey = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Order id cannot be empty.", nameof(id));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer id cannot be empty.", nameof(customerId));

        idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (idempotencyKey?.Length > 128)
        {
            throw new ArgumentException("Idempotency key cannot exceed 128 characters.", nameof(idempotencyKey));
        }

        Id = id;
        CustomerId = customerId;
        CreatedAtUtc = createdAtUtc;
        Status = status;
        IdempotencyKey = idempotencyKey;
    }

    public void AddItem(OrderItem item)
    {
        _items.Add(item);
    }

    public bool MarkPendingPayment()
    {
        if (Status == OrderStatus.PendingPayment)
        {
            return false;
        }

        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException($"Order cannot move from {Status} to {OrderStatus.PendingPayment}.");
        }

        Status = OrderStatus.PendingPayment;
        return true;
    }

    public bool MarkPaid()
    {
        if (Status == OrderStatus.Paid)
        {
            return false;
        }

        if (Status is OrderStatus.Cancelled or OrderStatus.PaymentFailed)
        {
            throw new InvalidOperationException($"Order cannot move from {Status} to {OrderStatus.Paid}.");
        }

        if (Status is not (OrderStatus.Pending or OrderStatus.PendingPayment))
        {
            throw new InvalidOperationException($"Order cannot be paid while it is {Status}.");
        }

        Status = OrderStatus.Paid;
        return true;
    }

    public bool MarkPaymentFailed()
    {
        if (Status is OrderStatus.PaymentFailed or OrderStatus.Cancelled)
        {
            return false;
        }

        if (Status == OrderStatus.Paid)
        {
            return false;
        }

        if (Status is not (OrderStatus.Pending or OrderStatus.PendingPayment))
        {
            throw new InvalidOperationException($"Order cannot fail payment while it is {Status}.");
        }

        Status = OrderStatus.PaymentFailed;
        return true;
    }

    public bool Cancel()
    {
        if (Status == OrderStatus.Cancelled)
        {
            return false;
        }

        if (Status == OrderStatus.Paid)
        {
            throw new InvalidOperationException("Paid order cannot be cancelled without a refund workflow.");
        }

        Status = OrderStatus.Cancelled;
        return true;
    }
}
