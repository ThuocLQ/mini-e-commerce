namespace PaymentService.Infrastructure.Outbox;

public sealed class OrderingSagaClientOptions
{
    public const string SectionName = "ServiceUrls";

    public string OrderingHttp { get; init; } = "https://localhost:7005";
}
