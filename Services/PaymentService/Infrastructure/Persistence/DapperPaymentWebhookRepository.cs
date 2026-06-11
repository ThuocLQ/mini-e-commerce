using Dapper;
using PaymentService.Application.Abstractions;
using PaymentService.Domain.Payments;

namespace PaymentService.Infrastructure.Persistence;

public sealed class DapperPaymentWebhookRepository : IPaymentWebhookRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperPaymentWebhookRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Payment?> ApplyAsync(
        string providerEventId,
        Guid paymentId,
        string providerTransactionId,
        PaymentStatus status,
        string? failureReason,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var inserted = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO WebhookLogs (
                Id,
                ProviderEventId,
                PaymentId,
                ProviderTransactionId,
                EventType,
                Status,
                Error,
                ReceivedAtUtc,
                ProcessedAtUtc)
            VALUES (
                @Id,
                @ProviderEventId,
                @PaymentId,
                @ProviderTransactionId,
                @EventType,
                'Processing',
                NULL,
                @ReceivedAtUtc,
                NULL)
            ON CONFLICT (ProviderEventId) DO NOTHING;
            """, new
        {
            Id = Guid.NewGuid(),
            ProviderEventId = providerEventId,
            PaymentId = paymentId,
            ProviderTransactionId = providerTransactionId.Trim(),
            EventType = status.ToString(),
            ReceivedAtUtc = receivedAtUtc
        }, transaction, cancellationToken: cancellationToken));

        if (inserted == 0)
        {
            var existingPayment = await GetPaymentAsync(paymentId, transaction, cancellationToken);
            transaction.Commit();
            return existingPayment;
        }

        var payment = await GetPaymentForUpdateAsync(paymentId, transaction, cancellationToken);

        if (payment is null)
        {
            await MarkWebhookFailedAsync(
                providerEventId,
                "Payment was not found.",
                DateTime.UtcNow,
                transaction,
                cancellationToken);

            transaction.Commit();
            return null;
        }

        try
        {
            if (status == PaymentStatus.Succeeded)
            {
                payment.MarkSucceeded(providerTransactionId, DateTime.UtcNow);
            }
            else
            {
                payment.MarkFailed(failureReason ?? "Payment failed by provider.", DateTime.UtcNow);
            }

            await UpdatePaymentAsync(payment, transaction, cancellationToken);
            await MarkWebhookProcessedAsync(providerEventId, DateTime.UtcNow, transaction, cancellationToken);

            transaction.Commit();
            return payment;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await MarkWebhookFailedAsync(
                providerEventId,
                ex.Message,
                DateTime.UtcNow,
                transaction,
                cancellationToken);

            transaction.Commit();
            throw;
        }
    }

    private static async Task<Payment?> GetPaymentAsync(
        Guid paymentId,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var row = await transaction.Connection!.QuerySingleOrDefaultAsync<PaymentRow>(new CommandDefinition("""
            SELECT Id, OrderId, CustomerId, Amount, Currency, Status, ProviderTransactionId, FailureReason, CreatedAtUtc, CompletedAtUtc
            FROM Payments
            WHERE Id = @PaymentId;
            """, new { PaymentId = paymentId }, transaction, cancellationToken: cancellationToken));

        return row is null ? null : MapPayment(row);
    }

    private static async Task<Payment?> GetPaymentForUpdateAsync(
        Guid paymentId,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var row = await transaction.Connection!.QuerySingleOrDefaultAsync<PaymentRow>(new CommandDefinition("""
            SELECT Id, OrderId, CustomerId, Amount, Currency, Status, ProviderTransactionId, FailureReason, CreatedAtUtc, CompletedAtUtc
            FROM Payments
            WHERE Id = @PaymentId
            FOR UPDATE;
            """, new { PaymentId = paymentId }, transaction, cancellationToken: cancellationToken));

        return row is null ? null : MapPayment(row);
    }

    private static async Task UpdatePaymentAsync(
        Payment payment,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.Connection!.ExecuteAsync(new CommandDefinition("""
            UPDATE Payments
            SET Status = @Status,
                ProviderTransactionId = @ProviderTransactionId,
                FailureReason = @FailureReason,
                CompletedAtUtc = @CompletedAtUtc
            WHERE Id = @Id;
            """, ToParameters(payment), transaction, cancellationToken: cancellationToken));
    }

    private static async Task MarkWebhookProcessedAsync(
        string providerEventId,
        DateTime processedAtUtc,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.Connection!.ExecuteAsync(new CommandDefinition("""
            UPDATE WebhookLogs
            SET Status = 'Processed',
                Error = NULL,
                ProcessedAtUtc = @ProcessedAtUtc
            WHERE ProviderEventId = @ProviderEventId;
            """, new
        {
            ProviderEventId = providerEventId,
            ProcessedAtUtc = processedAtUtc
        }, transaction, cancellationToken: cancellationToken));
    }

    private static async Task MarkWebhookFailedAsync(
        string providerEventId,
        string error,
        DateTime processedAtUtc,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.Connection!.ExecuteAsync(new CommandDefinition("""
            UPDATE WebhookLogs
            SET Status = 'Failed',
                Error = @Error,
                ProcessedAtUtc = @ProcessedAtUtc
            WHERE ProviderEventId = @ProviderEventId;
            """, new
        {
            ProviderEventId = providerEventId,
            Error = Truncate(error, 4000),
            ProcessedAtUtc = processedAtUtc
        }, transaction, cancellationToken: cancellationToken));
    }

    private static object ToParameters(Payment payment)
    {
        return new
        {
            payment.Id,
            Status = payment.Status.ToString(),
            payment.ProviderTransactionId,
            payment.FailureReason,
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

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
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
