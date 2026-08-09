namespace OrderingService.Application.Abstractions;

public interface IDiscountClient
{
    Task<DiscountApplicationResult> ApplyAsync(
        string couponCode,
        decimal orderAmount,
        CancellationToken cancellationToken = default);
}

public sealed record DiscountApplicationResult(
    string CouponCode,
    bool IsValid,
    decimal DiscountAmount,
    decimal FinalAmount,
    string Message);
