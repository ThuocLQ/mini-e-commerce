namespace NotificationWorker.Infrastructure.Notifications;

public sealed class NotificationDeliveryOptions
{
    public const string SectionName = "NotificationDelivery";
    public string Mode { get; init; } = "Logging";
    public SmtpOptions Smtp { get; init; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; }
    public string? UserName { get; init; }
    public string? Password { get; init; }
    public string FromAddress { get; init; } = string.Empty;
    public string FromDisplayName { get; init; } = "MicroShop";
    public string PublicStorefrontBaseUrl { get; init; } = string.Empty;
}