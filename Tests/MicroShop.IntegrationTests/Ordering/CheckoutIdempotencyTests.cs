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
            throwDuplicateOnCreate: true);
        var inventory = new RecordingInventoryReservationClient();
        var handler = CreateHandler(basket, repository, inventory);

        var result = await handler.Handle(
            new CheckoutCommand(customerId, "checkout-3", null, basket.BasketId, basket.Version),
            TestContext.Current.CancellationToken);

        Assert.Equal(existingOrder.Id, result.Id);
        var lostAttemptOrderId = Assert.Single(inventory.ReservedOrderIds);
        Assert.Equal(lostAttemptOrderId, Assert.Single(inventory.ReleasedOrderIds));
    }

    private static CheckoutHandler CreateHandler(
        BasketDto basket,
        IOrderRepository orderRepository,
        IInventoryReservationClient inventoryClient)
    {
        return new CheckoutHandler(
            new StubBasketClient(basket),
            orderRepository,
            new StubOutboxRepository(),
            new StubCatalogProductSnapshotClient(),
            new StubDiscountClient(),
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
    }

    private sealed class RecordingInventoryReservationClient : IInventoryReservationClient
    {
        public List<Guid> ReservedOrderIds { get; } = [];
        public List<Guid> ReleasedOrderIds { get; } = [];

        public Task<InventoryReservationResponse> ReserveAsync(Guid orderId, IReadOnlyList<InventoryReservationItem> items, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
        {
            ReservedOrderIds.Add(orderId);
            return Task.FromResult(new InventoryReservationResponse(true, null));
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
        bool throwDuplicateOnCreate = false) : IOrderRepository
    {
        private int _idempotencyReads;

        public Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Order>>([existingOrder]);
        public Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Order>>([existingOrder]);
        public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(id == existingOrder.Id ? existingOrder : null);
        public Task<Order?> GetByIdAsync(Guid id, IDbTransaction transaction, CancellationToken cancellationToken = default) => Task.FromResult<Order?>(id == existingOrder.Id ? existingOrder : null);
        public Task<Order?> GetByCustomerAndIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            _idempotencyReads++;
            return Task.FromResult<Order?>(getExistingOnInitialRead || _idempotencyReads > 1 ? existingOrder : null);
        }

        public Task<Order> CreateAsync(Order order, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            if (throwDuplicateOnCreate)
            {
                throw new OrderAlreadyExistsException(order.CustomerId, order.IdempotencyKey!);
            }

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
