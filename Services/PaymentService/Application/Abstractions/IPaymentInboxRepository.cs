using System.Data;

namespace PaymentService.Application.Abstractions;

public interface IPaymentInboxRepository
{
    Task<bool> TryRecordAsync(
        Guid eventId,
        string consumerName,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
