using Dapper;
using OrderingService.Application.Abstractions;
using OrderingService.Domain.OrderPaymentSagas;

namespace OrderingService.Infrastructure.Persistence;

public sealed class DapperOrderPaymentSagaRepository : IOrderPaymentSagaRepository
{
    public async Task<OrderPaymentSaga?> GetByOrderIdAsync(
        Guid orderId,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var row = await transaction.Connection!.QuerySingleOrDefaultAsync<OrderPaymentSagaRow>(new CommandDefinition("""
            SELECT Id, OrderId, PaymentId, State, StartedAtUtc, UpdatedAtUtc, TimeoutAtUtc, LastProcessedEventId, LastError
            FROM OrderPaymentSagas
            WHERE OrderId = @OrderId
            FOR UPDATE;
            """, new { OrderId = orderId }, transaction, cancellationToken: cancellationToken));

        return row is null ? null : Map(row);
    }

    public async Task UpsertAsync(
        OrderPaymentSaga saga,
        System.Data.IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await transaction.Connection!.ExecuteAsync(new CommandDefinition("""
            INSERT INTO OrderPaymentSagas (
                Id,
                OrderId,
                PaymentId,
                State,
                StartedAtUtc,
                UpdatedAtUtc,
                TimeoutAtUtc,
                LastProcessedEventId,
                LastError)
            VALUES (
                @Id,
                @OrderId,
                @PaymentId,
                @State,
                @StartedAtUtc,
                @UpdatedAtUtc,
                @TimeoutAtUtc,
                @LastProcessedEventId,
                @LastError)
            ON CONFLICT (OrderId) DO UPDATE
            SET PaymentId = EXCLUDED.PaymentId,
                State = EXCLUDED.State,
                UpdatedAtUtc = EXCLUDED.UpdatedAtUtc,
                TimeoutAtUtc = EXCLUDED.TimeoutAtUtc,
                LastProcessedEventId = EXCLUDED.LastProcessedEventId,
                LastError = EXCLUDED.LastError;
            """, ToParameters(saga), transaction, cancellationToken: cancellationToken));
    }

    private static object ToParameters(OrderPaymentSaga saga)
    {
        return new
        {
            saga.Id,
            saga.OrderId,
            saga.PaymentId,
            State = saga.State.ToString(),
            saga.StartedAtUtc,
            saga.UpdatedAtUtc,
            saga.TimeoutAtUtc,
            saga.LastProcessedEventId,
            saga.LastError
        };
    }

    private static OrderPaymentSaga Map(OrderPaymentSagaRow row)
    {
        return new OrderPaymentSaga(
            row.Id,
            row.OrderId,
            row.PaymentId,
            Enum.Parse<OrderPaymentSagaState>(row.State),
            row.StartedAtUtc,
            row.UpdatedAtUtc,
            row.TimeoutAtUtc,
            row.LastProcessedEventId,
            row.LastError);
    }

    private sealed record OrderPaymentSagaRow(
        Guid Id,
        Guid OrderId,
        Guid PaymentId,
        string State,
        DateTime StartedAtUtc,
        DateTime UpdatedAtUtc,
        DateTime TimeoutAtUtc,
        Guid? LastProcessedEventId,
        string? LastError);
}
