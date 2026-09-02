using System.Data;
using Dapper;
using PaymentService.Application.Abstractions;
using PaymentService.Domain.Payments;

namespace PaymentService.Infrastructure.Persistence;

public sealed class DapperPaymentOperationalActionRepository : IPaymentOperationalActionRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DapperPaymentOperationalActionRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task CreateAsync(
        PaymentOperationalAction action,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return transaction.Connection!.ExecuteAsync(new CommandDefinition("""
            INSERT INTO PaymentOperationalActions (
                Id, PaymentId, ActionType, RequestedBy, Reason, RequestedAtUtc, CompletedAtUtc, FailureReason)
            VALUES (
                @Id, @PaymentId, @ActionType, @RequestedBy, @Reason, @RequestedAtUtc, @CompletedAtUtc, @FailureReason);
            """, action, transaction, cancellationToken: cancellationToken));
    }

    public async Task CompleteLatestPendingAsync(
        Guid paymentId,
        string actionType,
        DateTime completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE PaymentOperationalActions
            SET CompletedAtUtc = @CompletedAtUtc,
                FailureReason = NULL
            WHERE Id = (
                SELECT Id
                FROM PaymentOperationalActions
                WHERE PaymentId = @PaymentId
                  AND ActionType = @ActionType
                  AND CompletedAtUtc IS NULL
                ORDER BY RequestedAtUtc DESC, Id DESC
                LIMIT 1
            );
            """, new
        {
            PaymentId = paymentId,
            ActionType = actionType.Trim(),
            CompletedAtUtc = completedAtUtc
        }, cancellationToken: cancellationToken));
    }
    public async Task<IReadOnlyList<PaymentOperationalAction>> GetByPaymentIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var actions = await connection.QueryAsync<PaymentOperationalAction>(new CommandDefinition("""
            SELECT Id, PaymentId, ActionType, RequestedBy, Reason, RequestedAtUtc, CompletedAtUtc, FailureReason
            FROM PaymentOperationalActions
            WHERE PaymentId = @PaymentId
            ORDER BY RequestedAtUtc DESC, Id DESC;
            """, new { PaymentId = paymentId }, cancellationToken: cancellationToken));

        return actions.AsList();
    }
}