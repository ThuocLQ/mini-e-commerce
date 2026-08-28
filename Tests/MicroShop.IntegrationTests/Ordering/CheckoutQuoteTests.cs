using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrderingService.Application.Abstractions;
using OrderingService.Application.Baskets;
using OrderingService.Application.Catalog;
using OrderingService.Application.Discounts;
using OrderingService.Application.IntegrationEvents;
using OrderingService.Application.Inventory;
using OrderingService.Application.Orders.CheckoutQuote;
using OrderingService.Infrastructure.CheckoutQuote;

namespace MicroShop.IntegrationTests.Ordering;

public sealed class CheckoutQuoteTests
{
    [Fact]
    public async Task Quote_UsesCurrentStateWithoutCreatingCouponOrInventoryReservations()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var basket = new BasketDto(
            customerId.ToString("D"),
            Guid.NewGuid(),
            [new BasketItemDto(productId.ToString("D"), "Old product name", 10m, 2)],
            3);
        var discount = new RecordingDiscountClient(isValid: true, discountAmount: 3m);
        var availability = new RecordingAvailabilityClient(available: true);
        var handler = CreateHandler(basket, new CatalogProductSnapshot(productId, "Current product name", 12m), discount, availability);

        var quote = await handler.Handle(
            new CheckoutQuoteCommand(customerId, basket.BasketId, basket.Version, "SAVE3", null),
            TestContext.Current.CancellationToken);

        Assert.True(quote.CanCheckout);
        Assert.NotNull(quote.QuoteToken);
        Assert.Equal(24m, quote.SubtotalAmount);
        Assert.Equal(3m, quote.DiscountAmount);
        Assert.Equal(21m, quote.TotalAmount);
        var item = Assert.Single(quote.Items);
        Assert.True(item.PriceChanged);
        Assert.True(item.Availability);
        Assert.Equal("Current product name", item.ProductName);
        Assert.Equal(12m, item.CurrentUnitPrice);
        Assert.Equal(1, discount.ApplyCalls);
        Assert.Equal(0, discount.ReserveCalls);
        Assert.Equal(1, availability.Calls);
    }

    [Fact]
    public async Task InvalidCoupon_ReturnsNonCheckoutableQuoteWithoutTokenOrReservations()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var basket = new BasketDto(
            customerId.ToString("D"),
            Guid.NewGuid(),
            [new BasketItemDto(productId.ToString("D"), "Product", 10m, 1)],
            1);
        var discount = new RecordingDiscountClient(isValid: false, discountAmount: 0m);
        var availability = new RecordingAvailabilityClient(available: true);
        var handler = CreateHandler(basket, new CatalogProductSnapshot(productId, "Product", 10m), discount, availability);

        var quote = await handler.Handle(
            new CheckoutQuoteCommand(customerId, basket.BasketId, basket.Version, "INVALID", null),
            TestContext.Current.CancellationToken);

        Assert.False(quote.CanCheckout);
        Assert.Null(quote.QuoteToken);
        Assert.Contains(quote.Issues, issue => issue.Code == "COUPON_INVALID");
        Assert.Equal(1, discount.ApplyCalls);
        Assert.Equal(0, discount.ReserveCalls);
        Assert.Equal(1, availability.Calls);
    }

    private static CheckoutQuoteHandler CreateHandler(
        BasketDto basket,
        CatalogProductSnapshot product,
        RecordingDiscountClient discount,
        RecordingAvailabilityClient availability)
    {
        var addressResolver = new CheckoutAddressSnapshotResolver(new NullAddressSnapshotClient());
        var evaluator = new CheckoutQuoteEvaluator(
            new StaticBasketClient(basket),
            new StaticCatalogProductClient(product),
            discount,
            availability,
            addressResolver,
            Options.Create(new OrderEventOptions { Currency = "USD" }),
            TimeProvider.System);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-jwt-key-that-is-long-enough-for-purpose-scoped-quote-signing"
            })
            .Build();
        var quoteOptions = Options.Create(new CheckoutQuoteOptions { LifetimeSeconds = 300 });
        var tokenService = new HmacCheckoutQuoteTokenService(quoteOptions, configuration, TimeProvider.System);

        return new CheckoutQuoteHandler(evaluator, tokenService, quoteOptions);
    }

    private sealed class StaticBasketClient(BasketDto basket) : IBasketClient
    {
        public Task<BasketDto?> GetBasketAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<BasketDto?>(basket);
        public Task<bool> TryClearBasketAsync(Guid customerId, long expectedVersion, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class StaticCatalogProductClient(CatalogProductSnapshot product) : ICatalogProductSnapshotClient
    {
        public Task<CatalogProductSnapshot?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CatalogProductSnapshot?>(product.ProductId == productId ? product : null);
    }

    private sealed class NullAddressSnapshotClient : IAddressSnapshotClient
    {
        public Task<CustomerAddressSnapshot?> GetAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CustomerAddressSnapshot?>(null);
    }

    private sealed class RecordingAvailabilityClient(bool available) : IInventoryAvailabilityClient
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<InventoryAvailabilityItem>> GetAvailabilityAsync(
            IReadOnlyList<InventoryAvailabilityRequestItem> items,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<InventoryAvailabilityItem>>(
                items.Select(item => new InventoryAvailabilityItem(item.ProductId, available)).ToList());
        }
    }

    private sealed class RecordingDiscountClient(bool isValid, decimal discountAmount) : IDiscountClient
    {
        public int ApplyCalls { get; private set; }
        public int ReserveCalls { get; private set; }

        public Task<DiscountApplicationResult> ApplyAsync(string couponCode, decimal orderAmount, CancellationToken cancellationToken = default)
        {
            ApplyCalls++;
            return Task.FromResult(new DiscountApplicationResult(
                couponCode,
                isValid,
                isValid ? discountAmount : 0m,
                isValid ? orderAmount - discountAmount : orderAmount,
                isValid ? "Coupon applied." : "Coupon is invalid."));
        }

        public Task<DiscountReservationResult> ReserveAsync(string couponCode, Guid orderId, Guid customerId, decimal orderAmount, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
        {
            ReserveCalls++;
            return Task.FromResult(new DiscountReservationResult(false, null, couponCode, 0m, orderAmount, "Unused by quote."));
        }

        public Task RedeemAsync(Guid reservationId, Guid orderId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReleaseAsync(Guid reservationId, Guid orderId, string reason, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
