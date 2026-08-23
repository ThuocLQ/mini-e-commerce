namespace OrderingService.Infrastructure.Sagas;

public sealed class PaymentSagaTimeoutOptions
{
    public const string SectionName = "PaymentSagaTimeout";

    public bool Enabled { get; init; } = true;
    public int IntervalSeconds { get; init; } = 15;
    public int BatchSize { get; init; } = 100;
}
