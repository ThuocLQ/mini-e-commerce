using PaymentService.Domain.Payments;

namespace PaymentService.Application.Abstractions;

public interface IPaymentRepository
{
    Task<Payment> CreateAsync(Payment payment, CancellationToken cancellationToken = default);
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Payment payment, CancellationToken cancellationToken = default);
}