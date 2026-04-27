using System.Text.Json;
using BasketService.Models;
using StackExchange.Redis;

namespace BasketService.Repositories;

public class RedisBasketRepository : IBasketRepository
{
    private readonly IDatabase _database;
    
    public RedisBasketRepository(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }
    
    public async Task<ShoppingCart> GetBasketAsync(string userId)
    {
        var data = await _database.StringGetAsync(GetKey(userId));
        if (data.IsNullOrEmpty)
        {
            return new ShoppingCart
            {
                UserId = userId
            };
        }

        return JsonSerializer.Deserialize<ShoppingCart>(data.ToString()!)
               ?? new ShoppingCart
               {
                   UserId = userId
               };
    }

    public async Task<ShoppingCart> AddItemToBasketAsync(string userId, BasketItem item)
    {
        var basket = await GetBasketAsync(userId);

        var existingItem = basket.Items.FirstOrDefault(x => x.ProductId == item.ProductId);

        if (existingItem is null)
        {
            basket.Items.Add(new BasketItem
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                Price = item.Price
            });
        }
        else
        {
            existingItem.Quantity += item.Quantity;
            existingItem.ProductName = item.ProductName;
            existingItem.Price = item.Price;
        }

        await UpdateBasketAsync(basket);

        return basket;
    }

    public async Task<ShoppingCart> UpdateBasketAsync(ShoppingCart cart)
    {
        var json = JsonSerializer.Serialize(cart);
        
        await _database.StringSetAsync(
            GetKey(cart.UserId), 
            json,
            expiry: TimeSpan.FromDays(7));

        return cart;
    }

    public async Task<ShoppingCart?> UpdateItemQuantityAsync(string userId, string productId, int quantity)
    {
        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be negative.");
        }

        var basket = await GetBasketAsync(userId);
        var existingItem = basket.Items.FirstOrDefault(x => x.ProductId == productId);

        if (existingItem is null)
        {
            return null;
        }

        if (quantity == 0)
        {
            basket.Items.Remove(existingItem);
            return await UpdateBasketAsync(basket);
        }

        existingItem.Quantity = quantity;

        return await UpdateBasketAsync(basket);
    }

    public async Task<bool> DeleteBasketAsync(string userId)
    {
        return await _database.KeyDeleteAsync(GetKey(userId));
    }

    public async Task<ShoppingCart> ClearBasketAsync(string userId)
    {
        var  basket = await GetBasketAsync(userId);
        basket.Items.Clear();
        return await UpdateBasketAsync(basket);
    }

    private static string GetKey(string userId)
    {
        return $"basket:{userId}";
    }
}
