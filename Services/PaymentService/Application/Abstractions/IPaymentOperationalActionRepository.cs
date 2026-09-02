using System.Data;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Abstractions;

public interface IPaymentOperationalActionRepository
{
    Task CreateAsync(
        PaymentOperationalAction action,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task CompleteLatestPendingAsync(
        Guid paymentId,
        string actionType,
        DateTime completedAtUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentOperationalAction>> GetByPaymentIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default);
}