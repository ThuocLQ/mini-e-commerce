namespace DiscountService.Domain.Discounts;

public sealed class DiscountStrategyFactory
{
    private readonly IReadOnlyDictionary<DiscountType, IDiscountStrategy> _strategiesByType;

    public DiscountStrategyFactory(IEnumerable<IDiscountStrategy> strategies)
    {
        _strategiesByType = strategies
            .GroupBy(strategy => strategy.Type)
            .ToDictionary(
                group => group.Key,
                group => group.Count() == 1
                    ? group.Single()
                    : throw new InvalidOperationException(
                        $"Multiple discount strategies were registered for type '{group.Key}'."));
    }

    public IDiscountStrategy GetStrategy(DiscountType type)
    {
        return _strategiesByType.TryGetValue(type, out var strategy)
            ? strategy
            : throw new InvalidOperationException($"Discount strategy for type '{type}' was not found.");
    }
}
