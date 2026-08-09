using System.Text.Json;
using BasketService.Application.Abstractions;
using BasketService.Domain.Baskets;
using StackExchange.Redis;

namespace BasketService.Infrastructure.Persistence;

public sealed class RedisBasketRepository : IBasketRepository
{
    private const string CompareAndSetScript = """
        local current = redis.call('GET', KEYS[1])
        if not current then
            if tonumber(ARGV[1]) ~= 0 then return 0 end
            redis.call('SET', KEYS[1], ARGV[2], 'EX', ARGV[3])
            return 1
        end
        local existing = cjson.decode(current)
        if tonumber(existing.Version) ~= tonumber(ARGV[1]) then return 0 end
        redis.call('SET', KEYS[1], ARGV[2], 'EX', ARGV[3])
        return 1
        """;

    private const string CompareAndDeleteScript = """
        local current = redis.call('GET', KEYS[1])
        if not current then return 0 end
        local existing = cjson.decode(current)
        if tonumber(existing.Version) ~= tonumber(ARGV[1]) then return 0 end
        return redis.call('DEL', KEYS[1])
        """;

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

    public async Task<ShoppingCart?> TryUpdateBasketAsync(
        ShoppingCart cart,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        cart.Version = checked(expectedVersion + 1);
        var json = JsonSerializer.Serialize(cart);
        var result = await _database.ScriptEvaluateAsync(
            CompareAndSetScript,
            [(RedisKey)GetKey(cart.UserId)],
            [expectedVersion, json, (int)TimeSpan.FromDays(7).TotalSeconds]);

        return result.ToString() == "1" ? cart : null;
    }

    public async Task<bool> DeleteBasketAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _database.KeyDeleteAsync(GetKey(userId));
    }

    public async Task<bool> TryDeleteBasketAsync(
        string userId,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _database.ScriptEvaluateAsync(
            CompareAndDeleteScript,
            [(RedisKey)GetKey(userId)],
            [expectedVersion]);

        return result.ToString() == "1";
    }

    private static string GetKey(string userId)
    {
        return $"basket:{userId}";
    }
}
