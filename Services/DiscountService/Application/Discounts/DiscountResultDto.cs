namespace DiscountService.Application.Discounts;

public sealed record DiscountResultDto(
    string CouponCode,
    bool IsValid,
    decimal OrderAmount,
    decimal DiscountAmount,
    decimal FinalAmount,
    string Message);

public sealed record CouponDto(
    string Code,
    string Type,
    decimal Value,
    DateTime ValidFromUtc,
    DateTime ValidToUtc,
    bool IsActive);