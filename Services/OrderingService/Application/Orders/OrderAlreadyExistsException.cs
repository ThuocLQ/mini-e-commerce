namespace OrderingService.Application.Orders;

public sealed class OrderAlreadyExistsException : Exception
{
    public OrderAlreadyExistsException(Guid customerId, string idempotencyKey)
        : base("An order with the same idempotency key already exists.")
    {
        CustomerId = customerId;
        IdempotencyKey = idempotencyKey;
    }

    public Guid CustomerId { get; }
    public string IdempotencyKey { get; }
}
