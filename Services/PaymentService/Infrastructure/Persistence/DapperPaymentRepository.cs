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
            Id = id.ToString()
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
            Id = payment.Id.ToString(),
            OrderId = payment.OrderId.ToString(),
            CustomerId = payment.CustomerId.ToString(),
            payment.Amount,
            payment.Currency,
            Status = payment.Status.ToString(),
            payment.ProviderTransactionId,
            payment.FailureReason,
            CreatedAtUtc = payment.CreatedAtUtc.ToString("O"),
            CompletedAtUtc = payment.CompletedAtUtc?.ToString("O")
        };
    }

    private static Payment MapPayment(PaymentRow row)
    {
        return new Payment(
            Guid.Parse(row.Id),
            Guid.Parse(row.OrderId),
            Guid.Parse(row.CustomerId),
            Convert.ToDecimal(row.Amount),
            row.Currency,
            Enum.Parse<PaymentStatus>(row.Status),
            DateTime.Parse(row.CreatedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
            row.ProviderTransactionId,
            row.FailureReason,
            row.CompletedAtUtc is null
                ? null
                : DateTime.Parse(row.CompletedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind));
    }

    private sealed record PaymentRow(
        string Id,
        string OrderId,
        string CustomerId,
        double Amount,
        string Currency,
        string Status,
        string? ProviderTransactionId,
        string? FailureReason,
        string CreatedAtUtc,
        string? CompletedAtUtc);
}
