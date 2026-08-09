using OrderingService.Domain.Orders;

namespace MicroShop.IntegrationTests.Ordering;

public sealed class OrderDiscountSnapshotTests
{
    [Fact]
    public void ApplyDiscount_PersistsCouponSnapshotAndReducesPaymentTotal()
    {
        var order = new Order(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            OrderStatus.PendingPayment);
        order.AddItem(new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Keyboard", 100m, 2));

        order.ApplyDiscount("save10", 25m);

        Assert.Equal("SAVE10", order.DiscountCode);
        Assert.Equal(25m, order.DiscountAmount);
        Assert.Equal(200m, order.SubtotalAmount);
        Assert.Equal(175m, order.TotalAmount);
    }

    [Fact]
    public void ApplyDiscount_RejectsAmountAboveSubtotal()
    {
        var order = new Order(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            OrderStatus.PendingPayment);
        order.AddItem(new OrderItem(Guid.NewGuid(), Guid.NewGuid(), "Keyboard", 100m, 1));

        Assert.Throws<ArgumentOutOfRangeException>(() => order.ApplyDiscount("SAVE10", 100.01m));
    }
}
