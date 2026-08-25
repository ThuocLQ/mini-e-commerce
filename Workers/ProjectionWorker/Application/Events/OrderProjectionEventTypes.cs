namespace ProjectionWorker.Application.Events;

public static class OrderProjectionEventTypes
{
    public const string OrderCreated = "OrderCreated";
    public const string OrderPaid = "OrderPaid";
    public const string OrderRefunded = "OrderRefunded";
    public const string OrderPaymentFailed = "OrderPaymentFailed";
    public const string OrderCancelled = "OrderCancelled";

    public static bool IsSupported(string? eventType)
    {
        return eventType is OrderCreated or OrderPaid or OrderRefunded or OrderPaymentFailed or OrderCancelled;
    }
}
