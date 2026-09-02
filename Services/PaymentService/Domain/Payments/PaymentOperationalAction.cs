namespace PaymentService.Domain.Payments;

public sealed record PaymentOperationalAction(
    Guid Id,
    Guid PaymentId,
    string ActionType,
    string RequestedBy,
    string Reason,
    DateTime RequestedAtUtc,
    DateTime? CompletedAtUtc = null,
    string? FailureReason = null)
{
    public static PaymentOperationalAction Create(
        Guid paymentId,
        string actionType,
        string requestedBy,
        string reason,
        DateTime requestedAtUtc) => new(
        Guid.NewGuid(),
        paymentId,
        Require(actionType, nameof(actionType)),
        Require(requestedBy, nameof(requestedBy)),
        Require(reason, nameof(reason)),
        requestedAtUtc);

    private static string Require(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", name) : value.Trim();
}