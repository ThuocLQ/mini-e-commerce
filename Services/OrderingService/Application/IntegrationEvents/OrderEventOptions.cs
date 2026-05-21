namespace OrderingService.Application.IntegrationEvents;

public sealed class OrderEventOptions
{
    public const string SectionName = "OrderEvents";

    public string Currency { get; init; } = "USD";
}
