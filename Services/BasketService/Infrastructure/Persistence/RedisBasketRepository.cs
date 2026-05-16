using System.Text.Json;
using BasketService.Application.Abstractions;
using BasketService.Domain.Baskets;
using StackExchange.Redis;

namespace BasketService.Infrastructure.Persistence;

public sealed class RedisBasketRepository : IBasketRepository
{
    private readonly IDatabase _database;

    public RedisBasketRepository(IConnectionMultiplexer redis)
    {
        _database = redis.GetDatabase();
    }

    public async Task<ShoppingCart> GetBasketAsync(string userId, CancellationToken cancellationToken = default)
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

    public async Task<ShoppingCart> UpdateBasketAsync(ShoppingCart cart, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(cart);

        await _database.StringSetAsync(
            GetKey(cart.UserId),
            json,
            expiry: TimeSpan.FromDays(7));

        return cart;
    }

    public async Task<bool> DeleteBasketAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _database.KeyDeleteAsync(GetKey(userId));
    }

    private static string GetKey(string userId)
    {
        return $"basket:{userId}";
    }
}
