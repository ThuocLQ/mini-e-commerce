namespace OrderingService.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    public Guid Id { get; }
    public Guid CustomerId { get; }
    public DateTime CreatedAtUtc { get; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items;
    public decimal TotalAmount => _items.Sum(item => item.TotalPrice);

    public Order(Guid id, Guid customerId, DateTime createdAtUtc, OrderStatus status)
    {
        if (id == Guid.Empty) throw new ArgumentException("Order id cannot be empty.", nameof(id));
        if (customerId == Guid.Empty) throw new ArgumentException("Customer id cannot be empty.", nameof(customerId));

        Id = id;
        CustomerId = customerId;
        CreatedAtUtc = createdAtUtc;
        Status = status;
    }

    public void AddItem(OrderItem item)
    {
        _items.Add(item);
    }
}