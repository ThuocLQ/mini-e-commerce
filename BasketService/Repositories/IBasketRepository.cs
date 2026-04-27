using BasketService.Models;

namespace BasketService.Repositories;

public interface IBasketRepository
{
    Task<ShoppingCart> GetBasketAsync(string userId);
    Task<ShoppingCart> AddItemToBasketAsync(string userId, BasketItem item);
    Task<ShoppingCart> UpdateBasketAsync(ShoppingCart cart);
    Task<ShoppingCart?> UpdateItemQuantityAsync(string userId, string productId, int quantity);
    Task<bool> DeleteBasketAsync(string userId);
    Task<ShoppingCart> ClearBasketAsync(string userId);
}
