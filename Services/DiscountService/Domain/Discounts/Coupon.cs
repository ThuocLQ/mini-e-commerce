namespace DiscountService.Domain.Discounts;

public sealed class Coupon
{
    public string Code { get; }
    public DiscountType Type { get; }
    public decimal Value { get; }
    public DateTime ValidFromUtc { get; }
    public DateTime ValidToUtc { get; }
    public bool IsActive { get; }

    public Coupon(
        string code,
        DiscountType type,
        decimal value,
        DateTime validFromUtc,
        DateTime validToUtc,
        bool isActive)
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

        Code = code.Trim().ToUpperInvariant();
        Type = type;
        Value = value;
        ValidFromUtc = validFromUtc;
        ValidToUtc = validToUtc;
        IsActive = isActive;
    }

    public bool CanBeUsedAt(DateTime utcNow)
    {
        return IsActive
               && utcNow >= ValidFromUtc
               && utcNow <= ValidToUtc;
    }
}