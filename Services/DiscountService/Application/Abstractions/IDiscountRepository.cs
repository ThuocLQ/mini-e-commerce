using DiscountService.Domain.Discounts;

namespace DiscountService.Application.Abstractions;

public interface IDiscountRepository
{
    Task<Coupon?> GetByCodeAsync(string code, CancellationToken cancellationToken = default); 
}