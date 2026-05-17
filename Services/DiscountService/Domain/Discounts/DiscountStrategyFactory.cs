namespace DiscountService.Domain.Discounts;

public sealed class DiscountStrategyFactory
{
    private readonly IReadOnlyList<IDiscountStrategy> _strategies;

    public DiscountStrategyFactory(IEnumerable<IDiscountStrategy> strategies)
    {
        _strategies = strategies.ToList();
    }

    public IDiscountStrategy GetStrategy(DiscountType type)
    {
        var strategy = _strategies.FirstOrDefault(strategy => strategy.Type == type);

        return strategy
               ?? throw new InvalidOperationException($"Discount strategy for type '{type}' was not found.");
    }
}