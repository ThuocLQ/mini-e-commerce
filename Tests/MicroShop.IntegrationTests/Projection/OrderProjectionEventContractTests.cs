using System.Text.Json;
using BuildingBlocks.Contracts.Events;
using BuildingBlocks.Contracts.Events.Orders;

namespace MicroShop.IntegrationTests.Projection;

public sealed class OrderProjectionEventContractTests
{
    [Fact]
    public void OrderProjectionEnvelope_v1_round_trips_without_losing_contract_fields()
    {
        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var envelope = new MicroShopEventEnvelope<OrderProjectionEventData>
        {
            EventId = eventId,
            EventType = "OrderCreated",
            EventVersion = 1,
            Source = "OrderingService",
            Subject = $"orders/{orderId:D}",
            Data = new OrderProjectionEventData
            {
                Sequence = 1,
                OrderId = orderId,
                CustomerId = customerId,
                CustomerName = customerId.ToString("D"),
                TotalAmount = 199.95m,
                Currency = "USD",
                ItemCount = 1,
                Items =
                [
                    new OrderProjectionItemData
                    {
                        ProductId = Guid.NewGuid(),
                        ProductName = "Demo product",
                        Quantity = 1,
                        UnitPrice = 199.95m
                    }
                ]
            }
        };

        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var restored = JsonSerializer.Deserialize<MicroShopEventEnvelope<OrderProjectionEventData>>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(restored);
        Assert.Equal(eventId, restored.EventId);
        Assert.Equal("OrderCreated", restored.EventType);
        Assert.Equal(1, restored.EventVersion);
        Assert.Equal(orderId, restored.Data.OrderId);
        Assert.Equal(1, restored.Data.Sequence);
        Assert.Equal(199.95m, restored.Data.TotalAmount);
    }
}
