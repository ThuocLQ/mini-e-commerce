namespace OrderingService.Application.Orders;

public sealed class InsufficientInventoryException : Exception
{
    public InsufficientInventoryException(string? message) : base(message ?? "One or more products are out of stock.") { }
}
