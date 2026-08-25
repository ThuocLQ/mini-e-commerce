namespace DiscountService.Domain.Discounts;

public sealed class Coupon
{
    public string Code { get; }
    public DiscountType Type { get; }
    public decimal Value { get; }
    public DateTime ValidFromUtc { get; }
    public DateTime ValidToUtc { get; }
    public bool IsActive { get; }
    public int? MaxRedemptions { get; }
    public int RedemptionCount { get; }

    public Coupon(
        string code,
        DiscountType type,
        decimal value,
        DateTime validFromUtc,
        DateTime validToUtc,
        bool isActive,
        int? maxRedemptions = null,
        int redemptionCount = 0)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Coupon code is required.", nameof(code));
        }

        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Coupon value must be greater than zero.");
        }

        if (validToUtc <= validFromUtc)
        {
            throw new ArgumentException("ValidToUtc must be greater than ValidFromUtc.");
        }

        if (maxRedemptions is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRedemptions), "Max redemptions must be greater than zero when supplied.");
        }

        if (redemptionCount < 0 || maxRedemptions is { } limit && redemptionCount > limit)
        {
            throw new ArgumentOutOfRangeException(nameof(redemptionCount), "Redemption count is outside the coupon capacity.");
        }

        Code = code.Trim().ToUpperInvariant();
        Type = type;
        Value = value;
        ValidFromUtc = validFromUtc;
        ValidToUtc = validToUtc;
        IsActive = isActive;
        MaxRedemptions = maxRedemptions;
        RedemptionCount = redemptionCount;
    }

    public bool CanBeUsedAt(DateTime utcNow)
    {
        return IsActive
               && utcNow >= ValidFromUtc
               && utcNow <= ValidToUtc;
    }

    public bool HasRemainingCapacity() => MaxRedemptions is null || RedemptionCount < MaxRedemptions;
}
