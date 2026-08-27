using BasketService.Application.Abstractions;
using BasketService.Application.Baskets;
using BasketService.Application.Baskets.AddBasketItem;
using BasketService.Application.Catalog;
using BasketService.Domain.Baskets;

namespace MicroShop.IntegrationTests.Basket;

public sealed class BasketCatalogFailureTests
{
    [Fact]
    public async Task AddItem_WhenCatalogProductIsAvailable_AddsTheRequestedPositiveQuantity()
    {
        var basketRepository = new RecordingBasketRepository();
        var handler = new AddBasketItemHandler(
            basketRepository,
            new AvailableCatalogProductClient());
        var productId = Guid.NewGuid().ToString("D");

        var basket = await handler.Handle(
            new AddBasketItemCommand("customer-1", productId, 1, CatalogCommunicationMode.Rest),
            TestContext.Current.CancellationToken);

        var item = Assert.Single(basket.Items);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal("Product", item.ProductName);
        Assert.Equal(1, item.Quantity);
        Assert.Equal(10m, item.Price);
        Assert.Equal(10m, basket.TotalPrice);
        Assert.Equal(1, basketRepository.GetCalls);
        Assert.Equal(1, basketRepository.UpdateCalls);
    }

    [Fact]
    public async Task AddItem_WhenCatalogProductIsUnknown_DoesNotMutateBasket()
    {
        var basketRepository = new RecordingBasketRepository();
        var handler = new AddBasketItemHandler(
            basketRepository,
            new UnknownCatalogProductClient());

        await Assert.ThrowsAsync<ProductNotFoundException>(() => handler.Handle(
            new AddBasketItemCommand("customer-1", Guid.NewGuid().ToString("D"), 1, CatalogCommunicationMode.Rest),
            TestContext.Current.CancellationToken));

        Assert.Equal(0, basketRepository.GetCalls);
        Assert.Equal(0, basketRepository.UpdateCalls);
    }

    [Fact]
    public async Task AddItem_WhenCatalogIsUnavailable_DoesNotMutateBasket()
    {
        var basketRepository = new RecordingBasketRepository();
        var handler = new AddBasketItemHandler(
            basketRepository,
            new UnavailableCatalogProductClient());

        var command = new AddBasketItemCommand(
            "customer-1",
            Guid.NewGuid().ToString("D"),
            1,
            CatalogCommunicationMode.Rest);

        await Assert.ThrowsAsync<CatalogUnavailableException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Equal(0, basketRepository.GetCalls);
        Assert.Equal(0, basketRepository.UpdateCalls);
    }

    [Fact]
    public async Task AddItem_WhenBasketVersionChanged_ThrowsConcurrencyException()
    {
        var handler = new AddBasketItemHandler(
            new ConflictingBasketRepository(),
            new AvailableCatalogProductClient());

        await Assert.ThrowsAsync<BasketConcurrencyException>(() => handler.Handle(
            new AddBasketItemCommand("customer-1", Guid.NewGuid().ToString("D"), 1, CatalogCommunicationMode.Rest),
            TestContext.Current.CancellationToken));
    }

    private sealed class UnavailableCatalogProductClient : ICatalogProductClient
    {
        public Task<CatalogProduct?> GetProductByIdAsync(
            string productId,
            CatalogCommunicationMode mode,
            CancellationToken cancellationToken = default)
        {
            throw new CatalogUnavailableException();
        }

        public Task<CatalogCallMeasurement> MeasureGetProductByIdAsync(
            string productId,
            CatalogCommunicationMode mode,
            CancellationToken cancellationToken = default)
        {
            throw new CatalogUnavailableException();
        }
    }

    private sealed class AvailableCatalogProductClient : ICatalogProductClient
    {
        public Task<CatalogProduct?> GetProductByIdAsync(
            string productId,
            CatalogCommunicationMode mode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CatalogProduct?>(new CatalogProduct(productId, "Product", 10m, null));
        }

        public Task<CatalogCallMeasurement> MeasureGetProductByIdAsync(
            string productId,
            CatalogCommunicationMode mode,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class UnknownCatalogProductClient : ICatalogProductClient
    {
        public Task<CatalogProduct?> GetProductByIdAsync(
            string productId,
            CatalogCommunicationMode mode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CatalogProduct?>(null);
        }

        public Task<CatalogCallMeasurement> MeasureGetProductByIdAsync(
            string productId,
            CatalogCommunicationMode mode,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingBasketRepository : IBasketRepository
    {
        public int GetCalls { get; private set; }
        public int UpdateCalls { get; private set; }

        public Task<ShoppingCart> GetBasketAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            GetCalls++;
            return Task.FromResult(new ShoppingCart { UserId = userId });
        }

        public Task<ShoppingCart?> TryUpdateBasketAsync(
            ShoppingCart cart,
            long expectedVersion,
            CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            return Task.FromResult<ShoppingCart?>(cart);
        }

        public Task<bool> DeleteBasketAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> TryDeleteBasketAsync(
            string userId,
            long expectedVersion,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class ConflictingBasketRepository : IBasketRepository
    {
        public Task<ShoppingCart> GetBasketAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ShoppingCart { UserId = userId, Version = 3 });
        }

        public Task<ShoppingCart?> TryUpdateBasketAsync(ShoppingCart cart, long expectedVersion, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ShoppingCart?>(null);
        }

        public Task<bool> DeleteBasketAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<bool> TryDeleteBasketAsync(string userId, long expectedVersion, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }
}
