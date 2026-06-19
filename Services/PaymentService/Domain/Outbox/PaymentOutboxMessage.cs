namespace PaymentService.Domain.Outbox;

public sealed class PaymentOutboxMessage
{
    public Guid Id { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Status { get; init; } = "Pending";
    public int RetryCount { get; init; }
    public string? Error { get; init; }
    public DateTime NextAttemptAtUtc { get; init; }
    public DateTime? ProcessedAtUtc { get; init; }
}
