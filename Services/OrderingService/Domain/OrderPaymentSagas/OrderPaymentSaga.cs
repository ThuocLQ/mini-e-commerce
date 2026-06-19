namespace OrderingService.Domain.OrderPaymentSagas;

public sealed class OrderPaymentSaga
{
    public Guid Id { get; }
    public Guid OrderId { get; }
    public Guid PaymentId { get; private set; }
    public OrderPaymentSagaState State { get; private set; }
    public DateTime StartedAtUtc { get; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime TimeoutAtUtc { get; }
    public Guid? LastProcessedEventId { get; private set; }
    public string? LastError { get; private set; }

    public OrderPaymentSaga(
        Guid id,
        Guid orderId,
        Guid paymentId,
        OrderPaymentSagaState state,
        DateTime startedAtUtc,
        DateTime updatedAtUtc,
        DateTime timeoutAtUtc,
        Guid? lastProcessedEventId = null,
        string? lastError = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Saga id cannot be empty.", nameof(id));
        if (orderId == Guid.Empty) throw new ArgumentException("Order id cannot be empty.", nameof(orderId));
        if (paymentId == Guid.Empty) throw new ArgumentException("Payment id cannot be empty.", nameof(paymentId));
        if (timeoutAtUtc <= startedAtUtc) throw new ArgumentException("Timeout must be after saga start.", nameof(timeoutAtUtc));

        Id = id;
        OrderId = orderId;
        PaymentId = paymentId;
        State = state;
        StartedAtUtc = startedAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        TimeoutAtUtc = timeoutAtUtc;
        LastProcessedEventId = lastProcessedEventId;
        LastError = string.IsNullOrWhiteSpace(lastError) ? null : lastError.Trim();
    }

    public static OrderPaymentSaga Start(
        Guid orderId,
        Guid paymentId,
        DateTime startedAtUtc,
        TimeSpan timeout)
    {
        return new OrderPaymentSaga(
            Guid.NewGuid(),
            orderId,
            paymentId,
            OrderPaymentSagaState.PaymentRequested,
            startedAtUtc,
            startedAtUtc,
            startedAtUtc.Add(timeout));
    }

    public bool HasProcessed(Guid eventId)
    {
        return LastProcessedEventId == eventId;
    }

    public void MarkOrderPaid(Guid eventId, DateTime updatedAtUtc)
    {
        State = OrderPaymentSagaState.OrderPaid;
        MarkProcessed(eventId, updatedAtUtc, null);
    }

    public void MarkOrderCancelled(Guid eventId, DateTime updatedAtUtc, string? reason)
    {
        State = OrderPaymentSagaState.OrderCancelled;
        MarkProcessed(eventId, updatedAtUtc, reason);
    }

    public void MarkTimedOut(Guid eventId, DateTime updatedAtUtc, string reason)
    {
        State = OrderPaymentSagaState.TimedOut;
        MarkProcessed(eventId, updatedAtUtc, reason);
    }

    public void MarkCompensationRequired(Guid eventId, DateTime updatedAtUtc, string reason)
    {
        State = OrderPaymentSagaState.CompensationRequired;
        MarkProcessed(eventId, updatedAtUtc, reason);
    }

    public void RecordIgnoredEvent(Guid eventId, DateTime updatedAtUtc, string? note = null)
    {
        MarkProcessed(eventId, updatedAtUtc, note);
    }

    private void MarkProcessed(Guid eventId, DateTime updatedAtUtc, string? note)
    {
        LastProcessedEventId = eventId;
        LastError = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        UpdatedAtUtc = updatedAtUtc;
    }
}
