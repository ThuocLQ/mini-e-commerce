namespace PaymentService.Application.Payments.Providers;

public sealed class PaymentProviderOptions
{
    public const string SectionName = "PaymentProvider";

    // Sandbox is intentionally available only to Development/Portfolio hosts.
    public string Provider { get; init; } = string.Empty;
    public int SandboxActionExpiryMinutes { get; init; } = 30;
}
