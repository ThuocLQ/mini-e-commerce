namespace OrderingService.Domain.Orders;

public sealed class OrderItem
{
    public Guid Id { get; }
    public Guid ProductId { get; }
    public string ProductName { get; }
    public decimal UnitPrice { get; }
    public int Quantity { get; }

    public decimal TotalPrice => UnitPrice * Quantity;

    public OrderItem(Guid id, Guid productId, string productName, decimal unitPrice, int quantity)
    {
        if (id == Guid.Empty) throw new ArgumentException("Order item id cannot be empty.", nameof(id));
        if (productId == Guid.Empty) throw new ArgumentException("Product id cannot be empty.", nameof(productId));
        if (string.IsNullOrWhiteSpace(productName)) throw new ArgumentException("Product name is required.", nameof(productName));
        if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");

        Id = id;
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }
}