using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderingService.Application.Abstractions;
using OrderingService.Application.Baskets;
using OrderingService.Application.IntegrationEvents;
using OrderingService.Application.Inventory;
using OrderingService.Application.Orders;
using OrderingService.Application.Orders.Checkout;
using OrderingService.Domain.Orders;
using OrderingService.Domain.Outbox;

namespace MicroShop.IntegrationTests.Ordering;

public sealed class CheckoutIdempotencyTests
{
    [Fact]
    public async Task MatchingReplay_ReturnsExistingOrderWithoutReservingInventory()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var basket = CreateBasket(customerId, productId, quantity: 2);
        var hash = CreateHash(basket, couponCode: null);
        var existingOrder = CreateOrder(customerId, productId, "checkout-1", hash, basket.BasketId);
        var inventory = new RecordingInventoryReservationClient();

        var handler = CreateHandler(
            basket,
            new StubOrderRepository(existingOrder),
            inventory);

        var result = await handler.Handle(
            new CheckoutCommand(customerId, "checkout-1", null, basket.BasketId, basket.Version),
            TestContext.Current.CancellationToken);

        Assert.Equal(existingOrder.Id, result.Id);
        Assert.Empty(inventory.ReservedOrderIds);
        Assert.Empty(inventory.ReleasedOrderIds);
    }

    [Fact]
    public async Task SameKeyWithDifferentBasketIdentity_ThrowsConflictWithoutReservingInventory()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var existingOrder = CreateOrder(customerId, productId, "checkout-2", new string('a', 64), Guid.NewGuid());
        var inventory = new RecordingInventoryReservationClient();

        var handler = CreateHandler(
            CreateBasket(customerId, productId, quantity: 2),
            new StubOrderRepository(existingOrder),
            inventory);

        await Assert.ThrowsAsync<CheckoutIdempotencyConflictException>(() => handler.Handle(
            new CheckoutCommand(customerId, "checkout-2", null, Guid.NewGuid(), BasketVersion: 1),
            TestContext.Current.CancellationToken));

        Assert.Empty(inventory.ReservedOrderIds);
        Assert.Empty(inventory.ReleasedOrderIds);
    }

    [Fact]
    public async Task SameBasketVersionWithNewKey_ReturnsExistingOrderWithoutReservingInventory()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var basket = CreateBasket(customerId, productId, quantity: 2);
        var existingOrder = CreateOrder(customerId, productId, "checkout-original", CreateHash(basket, couponCode: null), basket.BasketId);
        var inventory = new RecordingInventoryReservationClient();
        var handler = CreateHandler(basket, new StubOrderRepository(existingOrder), inventory);

        var result = await handler.Handle(
            new CheckoutCommand(customerId, "checkout-new-key", null, basket.BasketId, basket.Version),
            TestContext.Current.CancellationToken);

        Assert.Equal(existingOrder.Id, result.Id);
        Assert.Empty(inventory.ReservedOrderIds);
        Assert.Empty(inventory.ReleasedOrderIds);
    }

    [Fact]
    public async Task LegacyOrderWithoutRequestFingerprint_RequiresANewIdempotencyKey()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var existingOrder = new Order(
            Guid.NewGuid(),
            customerId,
            DateTime.UtcNow,
            OrderStatus.PendingPayment,
            "legacy-checkout-key");
        existingOrder.AddItem(new OrderItem(Guid.NewGuid(), productId, "Product", 10m, 1));
        var inventory = new RecordingInventoryReservationClient();
        var handler = CreateHandler(
            CreateBasket(customerId, productId, quantity: 1),
            new StubOrderRepository(existingOrder),
            inventory);

        await Assert.ThrowsAsync<CheckoutIdempotencyConflictException>(() => handler.Handle(
            new CheckoutCommand(customerId, "legacy-checkout-key", null, Guid.NewGuid(), BasketVersion: 1),
            TestContext.Current.CancellationToken));

        Assert.Empty(inventory.ReservedOrderIds);
    }

    [Fact]
    public async Task ConcurrentReplay_ReleasesReservationCreatedByLosingAttempt()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var basket = CreateBasket(customerId, productId, quantity: 2);
        var hash = CreateHash(basket, couponCode: null);
        var existingOrder = CreateOrder(customerId, productId, "checkout-3", hash, basket.BasketId);
        var repository = new StubOrderRepository(
            existingOrder,
            getExistingOnInitialRead: false,
            throwDuplicateOnCreate: true,
            getExistingByBasketOnInitialRead: false);
        var inventory = new RecordingInventoryReservationClient();
        var handler = CreateHandler(basket, repository, inventory);

        var result = await handler.Handle(
            new CheckoutCommand(customerId, "checkout-3", null, basket.BasketId, basket.Version),
            TestContext.Current.CancellationToken);

        Assert.Equal(existingOrder.Id, result.Id);
        var lostAttemptOrderId = Assert.Single(inventory.ReservedOrderIds);
        Assert.Equal(lostAttemptOrderId, Assert.Single(inventory.ReleasedOrderIds));
    }

    [Fact]
    public async Task CouponReservation_IsAttachedToTheCreatedOrder()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var basket = CreateBasket(customerId, productId, quantity: 1);
        var repository = new StubOrderRepository(CreateOrder(customerId, productId, "other-checkout", new string('a', 64), Guid.NewGuid()));
        var discount = new RecordingDiscountClient(isReserved: true);
        var handler = CreateHandler(basket, repository, new RecordingInventoryReservationClient(), discount);

        await handler.Handle(
            new CheckoutCommand(customerId, "coupon-checkout", "SAVE10", basket.BasketId, basket.Version),
            TestContext.Current.CancellationToken);

        Assert.Equal(discount.ReservationId, repository.LastCreatedOrder!.DiscountReservationId);
        Assert.Equal("SAVE10", repository.LastCreatedOrder.DiscountCode);
        Assert.Equal(9m, repository.LastCreatedOrder.TotalAmount);
    }

    [Fact]
    public async Task InventoryRejection_ReleasesTheCouponReservation()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var basket = CreateBasket(customerId, productId, quantity: 1);
        var repository = new StubOrderRepository(CreateOrder(customerId, productId, "other-checkout", new string('a', 64), Guid.NewGuid()));
        var discount = new RecordingDiscountClient(isReserved: true);
        var handler = CreateHandler(
            basket,
            repository,
            new RecordingInventoryReservationClient(succeeds: false),
            discount);

        await Assert.ThrowsAsync<InsufficientInventoryException>(() => handler.Handle(
            new CheckoutCommand(customerId, "coupon-inventory-rejected", "SAVE10", basket.BasketId, basket.Version),
            TestContext.Current.CancellationToken));

        Assert.Equal((discount.ReservationId, "Inventory reservation was rejected."), Assert.Single(discount.Releases));
    }

    private static CheckoutHandler CreateHandler(
        BasketDto basket,
        IOrderRepository orderRepository,
        IInventoryReservationClient inventoryClient,
        IDiscountClient? discountClient = null)
    {
        return new CheckoutHandler(
            new StubBasketClient(basket),
            orderRepository,
            new StubOutboxRepository(),
            new StubCatalogProductSnapshotClient(),
            discountClient ?? new StubDiscountClient(),
            inventoryClient,
            new InlineUnitOfWork(),
            Options.Create(new OrderEventOptions { Currency = "USD" }),
            NullLogger<CheckoutHandler>.Instance);
    }

    private static BasketDto CreateBasket(Guid customerId, Guid productId, int quantity) =>
        new(customerId.ToString("D"), Guid.NewGuid(), [new BasketItemDto(productId.ToString("D"), "Product", 10m, quantity)], 1);

    private static Order CreateOrder(Guid customerId, Guid productId, string idempotencyKey, string requestHash, Guid basketId)
    {
        var order = new Order(
            Guid.NewGuid(),
            customerId,
            DateTime.UtcNow,
            OrderStatus.PendingPayment,
            idempotencyKey,
            "USD",
            requestHash,
            checkoutBasketVersion: 1,
            checkoutBasketId: basketId);
        order.AddItem(new OrderItem(Guid.NewGuid(), productId, "Product", 10m, 2));
        return order;
    }

    private static string CreateHash(BasketDto basket, string? couponCode)
    {
        var coupon = string.IsNullOrWhiteSpace(couponCode) ? string.Empty : couponCode.Trim().ToUpperInvariant();
        var items = basket.Items
            .GroupBy(item => item.ProductId.Trim().ToUpperInvariant(), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}:{group.Sum(item => item.Quantity)}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"basketId={basket.BasketId:D}\nbasketVersion={basket.Version}\ncoupon={coupon}\nitems={string.Join(',', items)}"))).ToLowerInvariant();
    }

    private sealed class StubBasketClient(BasketDto basket) : IBasketClient
    {
        public Task<BasketDto?> GetBasketAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<BasketDto?>(basket);
        public Task<bool> TryClearBasketAsync(Guid customerId, long expectedVersion, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class StubCatalogProductSnapshotClient : ICatalogProductSnapshotClient
    {
        public Task<CatalogProductSnapshot?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CatalogProductSnapshot?>(new CatalogProductSnapshot(productId, "Product", 10m));
    }

    private sealed class StubDiscountClient : IDiscountClient
    {
        public Task<DiscountApplicationResult> ApplyAsync(string couponCode, decimal orderAmount, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DiscountApplicationResult(couponCode, false, 0m, orderAmount, "Coupon is not configured for this test."));

        public Task<DiscountReservationResult> ReserveAsync(string couponCode, Guid orderId, Guid customerId, decimal orderAmount, DateTime expiresAtUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DiscountReservationResult(false, null, couponCode, 0m, orderAmount, "Coupon is not configured for this test."));

        public Task RedeemAsync(Guid reservationId, Guid orderId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReleaseAsync(Guid reservationId, Guid orderId, string reason, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingDiscountClient(bool isReserved) : IDiscountClient
    {
        public Guid ReservationId { get; } = Guid.NewGuid();
        public List<(Guid ReservationId, string Reason)> Releases { get; } = [];

        public Task<DiscountApplicationResult> ApplyAsync(string couponCode, decimal orderAmount, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DiscountApplicationResult(couponCode, false, 0m, orderAmount, "Unused by checkout."));

        public Task<DiscountReservationResult> ReserveAsync(string couponCode, Guid orderId, Guid customerId, decimal orderAmount, DateTime expiresAtUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DiscountReservationResult(isReserved, isReserved ? ReservationId : null, couponCode, isReserved ? 1m : 0m, isReserved ? orderAmount - 1m : orderAmount, isReserved ? "Reserved." : "Rejected."));

        public Task RedeemAsync(Guid reservationId, Guid orderId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ReleaseAsync(Guid reservationId, Guid orderId, string reason, CancellationToken cancellationToken = default)
        {
            Releases.Add((reservationId, reason));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingInventoryReservationClient(bool succeeds = true) : IInventoryReservationClient
    {
        public List<Guid> ReservedOrderIds { get; } = [];
        public List<Guid> ReleasedOrderIds { get; } = [];

        public Task<InventoryReservationResponse> ReserveAsync(Guid orderId, IReadOnlyList<InventoryReservationItem> items, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
        {
            ReservedOrderIds.Add(orderId);
            return Task.FromResult(new InventoryReservationResponse(succeeds, succeeds ? null : "Insufficient stock."));
        }

        public Task ReleaseAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            ReleasedOrderIds.Add(orderId);
            return Task.CompletedTask;
        }

        public Task CommitAsync(Guid orderId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubOrderRepository(
        Order existingOrder,
        bool getExistingOnInitialRead = true,
        bool throwDuplicateOnCreate = false,
        bool getExistingByBasketOnInitialRead = true) : IOrderRepository
    {
        private int _idempotencyReads;
        private bool _createAttempted;
        public Order? LastCreatedOrder { get; private set; }

        public Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Order>>([existingOrder]);
        public Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Order>>([existingOrder]);
        public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(id == existingOrder.Id ? existingOrder : null);
        public Task<Order?> GetByIdAsync(Guid id, IDbTransaction transaction, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(id == existingOrder.Id ? existingOrder : null);
        public Task<Order?> GetByCustomerAndIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            _idempotencyReads++;
            var isMatchingKey = string.Equals(existingOrder.IdempotencyKey, idempotencyKey, StringComparison.Ordinal);
            return Task.FromResult<Order?>(
                isMatchingKey && (getExistingOnInitialRead || _idempotencyReads > 1) ? existingOrder : null);
        }

        public Task<Order?> GetByCustomerAndCheckoutBasketAsync(
            Guid customerId,
            Guid basketId,
            long basketVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Order?>(
                customerId == existingOrder.CustomerId &&
                basketId == existingOrder.CheckoutBasketId &&
                basketVersion == existingOrder.CheckoutBasketVersion &&
                (getExistingByBasketOnInitialRead || _createAttempted)
                    ? existingOrder
                    : null);

        public Task<Order> CreateAsync(Order order, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            _createAttempted = true;
            if (throwDuplicateOnCreate)
            {
                throw new OrderAlreadyExistsException(order.CustomerId, order.IdempotencyKey!);
            }

            LastCreatedOrder = order;
            return Task.FromResult(order);
        }

        public Task<bool> TryUpdateStatusAsync(Guid orderId, OrderStatus newStatus, IReadOnlyCollection<OrderStatus> expectedCurrentStatuses, IDbTransaction? transaction = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class StubOutboxRepository : IOutboxRepository
    {
        public Task AddAsync(OutboxMessage message, IDbTransaction? transaction = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(int batchSize, int maxRetryCount, Guid lockId, DateTime nowUtc, TimeSpan lockDuration, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<OutboxMessage>>([]);
        public Task<IReadOnlyList<OutboxMessage>> GetLatestAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<OutboxMessage>>([]);
        public Task MarkAsProcessedAsync(Guid id, Guid lockId, DateTime processedAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkAsFailedAsync(Guid id, Guid lockId, string error, DateTime nextAttemptAtUtc, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InlineUnitOfWork : IOrderingUnitOfWork
    {
        public Task<T> ExecuteAsync<T>(Func<IDbTransaction, Task<T>> operation, CancellationToken cancellationToken = default) => operation(new StubTransaction());
    }

    private sealed class StubTransaction : IDbTransaction
    {
        public IDbConnection? Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() { }
        public void Rollback() { }
        public void Dispose() { }
    }
}
