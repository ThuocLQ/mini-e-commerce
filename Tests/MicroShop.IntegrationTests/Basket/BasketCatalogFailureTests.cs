using BasketService.Application.Abstractions;
using BasketService.Application.Baskets.AddBasketItem;
using BasketService.Application.Catalog;
using BasketService.Domain.Baskets;

namespace MicroShop.IntegrationTests.Basket;

public sealed class BasketCatalogFailureTests
{
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

        public Task<ShoppingCart> UpdateBasketAsync(
            ShoppingCart cart,
            CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            return Task.FromResult(cart);
        }

        public Task<bool> DeleteBasketAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}
