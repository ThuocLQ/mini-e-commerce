using Dapper;
using PaymentService.Application.Abstractions;
using PaymentService.Domain.Payments;

namespace PaymentService.Infrastructure.Persistence;

public sealed class DapperPaymentRepository : IPaymentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperPaymentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Payment> CreateAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO Payments (
                Id,
                OrderId,
                CustomerId,
                Amount,
                Currency,
                Status,
                ProviderTransactionId,
                FailureReason,
                CreatedAtUtc,
                CompletedAtUtc)
            VALUES (
                @Id,
                @OrderId,
                @CustomerId,
                @Amount,
                @Currency,
                @Status,
                @ProviderTransactionId,
                @FailureReason,
                @CreatedAtUtc,
                @CompletedAtUtc);
            """, ToParameters(payment), cancellationToken: cancellationToken));

        return payment;
    }

    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<PaymentRow>(new CommandDefinition("""
            SELECT Id, OrderId, CustomerId, Amount, Currency, Status, ProviderTransactionId, FailureReason, CreatedAtUtc, CompletedAtUtc
            FROM Payments
            WHERE Id = @Id;
            """, new
        {
            Id = id
        }, cancellationToken: cancellationToken));

        return row is null ? null : MapPayment(row);
    }

    public async Task<bool> UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var affectedRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Payments
            SET Status = @Status,
                ProviderTransactionId = @ProviderTransactionId,
                FailureReason = @FailureReason,
                CompletedAtUtc = @CompletedAtUtc
            WHERE Id = @Id;
            """, ToParameters(payment), cancellationToken: cancellationToken));

        return affectedRows > 0;
    }

    private static object ToParameters(Payment payment)
    {
        return new
        {
            payment.Id,
            payment.OrderId,
            payment.CustomerId,
            payment.Amount,
            payment.Currency,
            Status = payment.Status.ToString(),
            payment.ProviderTransactionId,
            payment.FailureReason,
            payment.CreatedAtUtc,
            payment.CompletedAtUtc
        };
    }

    private static Payment MapPayment(PaymentRow row)
    {
        return new Payment(
            row.Id,
            row.OrderId,
            row.CustomerId,
            row.Amount,
            row.Currency,
            Enum.Parse<PaymentStatus>(row.Status),
            row.CreatedAtUtc,
            row.ProviderTransactionId,
            row.FailureReason,
            row.CompletedAtUtc);
    }

    private sealed record PaymentRow(
        Guid Id,
        Guid OrderId,
        Guid CustomerId,
        decimal Amount,
        string Currency,
        string Status,
        string? ProviderTransactionId,
        string? FailureReason,
        DateTime CreatedAtUtc,
        DateTime? CompletedAtUtc);
}
