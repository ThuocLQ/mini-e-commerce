using OrderingService.Domain.Fulfillment;
namespace MicroShop.IntegrationTests.Ordering;
public sealed class ShipmentDomainTests
{
 [Fact] public void Shipment_RequiresTrackingBeforeDispatch_AndTransitionsSequentially(){var shipment=Shipment.Create(Guid.NewGuid(),DateTime.UtcNow);Assert.Equal(ShipmentStatus.ReadyToShip,shipment.Status);Assert.Throws<ArgumentException>(()=>shipment.Dispatch("","",DateTime.UtcNow));Assert.True(shipment.Dispatch("DHL","DHL-100",DateTime.UtcNow));Assert.True(shipment.Deliver(DateTime.UtcNow));Assert.False(shipment.Deliver(DateTime.UtcNow));Assert.Equal(ShipmentStatus.Delivered,shipment.Status);}
 [Fact] public void Shipment_CannotDeliverBeforeDispatch(){var shipment=Shipment.Create(Guid.NewGuid(),DateTime.UtcNow);Assert.Throws<InvalidOperationException>(()=>shipment.Deliver(DateTime.UtcNow));}
}