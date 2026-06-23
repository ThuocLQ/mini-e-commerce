namespace ApiGateway;

public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    public string[] AllowedCorsOrigins { get; init; } = [];
    public bool BlockDebugRoutesOutsideDevelopment { get; init; } = true;
    public int GeneralPermitLimit { get; init; } = 120;
    public int WebhookPermitLimit { get; init; } = 60;
    public int HealthPermitLimit { get; init; } = 600;
    public int WindowSeconds { get; init; } = 60;
}
