using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using NotificationWorker.Application.Abstractions;
using NotificationWorker.Infrastructure.Identity;

namespace NotificationWorker.Infrastructure.Notifications;

public sealed class SmtpNotificationSender : INotificationSender
{
    private readonly ICustomerContactClient _contacts;
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpNotificationSender> _logger;

    public SmtpNotificationSender(ICustomerContactClient contacts, IOptions<NotificationDeliveryOptions> options, ILogger<SmtpNotificationSender> logger)
    {
        _contacts = contacts;
        _options = options.Value.Smtp;
        _logger = logger;
    }

    public Task SendOrderCreatedAsync(OrderCreatedNotification notification, CancellationToken cancellationToken) =>
        SendLifecycleAsync(notification.CustomerId, $"Order {notification.OrderId:D} received", $"We received your order {notification.OrderId:D}. Total: {notification.TotalAmount:0.00} {notification.Currency}.", notification.EventId, notification.OrderId, cancellationToken);

    public Task SendOrderStatusChangedAsync(OrderStatusChangedNotification notification, CancellationToken cancellationToken) =>
        SendLifecycleAsync(notification.CustomerId, $"Order {notification.OrderId:D} is {notification.CurrentStatus}", $"Your order {notification.OrderId:D} changed from {notification.PreviousStatus} to {notification.CurrentStatus}. Total: {notification.TotalAmount:0.00} {notification.Currency}.", notification.EventId, notification.OrderId, cancellationToken);

    public async Task SendEmailVerificationAsync(EmailVerificationNotification notification, CancellationToken cancellationToken)
    {
        var contact = await _contacts.GetAsync(notification.CustomerId, cancellationToken);
        if (contact is null) { _logger.LogWarning("Skipping verification email because customer contact is unavailable. EventId={EventId}", notification.EventId); return; }
        var link = $"{_options.PublicStorefrontBaseUrl.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(notification.Token)}";
        await SendMessageAsync(contact.Email, "Verify your MicroShop email", $"Verify your email before receiving order updates: {link}\n\nThis link expires at {notification.ExpiresAtUtc:O}.", notification.EventId, notification.CustomerId, null, cancellationToken);
    }

    private async Task SendLifecycleAsync(Guid customerId, string subject, string body, Guid eventId, Guid orderId, CancellationToken cancellationToken)
    {
        var contact = await _contacts.GetAsync(customerId, cancellationToken);
        if (contact is null || !contact.IsEmailVerified || !contact.ReceivesOrderUpdates)
        {
            _logger.LogInformation("Skipping lifecycle email notification because customer contact is unavailable, unverified, or opted out. EventId={EventId}, OrderId={OrderId}, CustomerId={CustomerId}", eventId, orderId, customerId);
            return;
        }
        await SendMessageAsync(contact.Email, subject, body, eventId, customerId, orderId, cancellationToken);
    }

    private async Task SendMessageAsync(string email, string subject, string body, Guid eventId, Guid customerId, Guid? orderId, CancellationToken cancellationToken)
    {
        using var message = new MailMessage { From = new MailAddress(_options.FromAddress, _options.FromDisplayName), Subject = subject, Body = body, IsBodyHtml = false };
        message.To.Add(email);
        using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.EnableSsl, DeliveryMethod = SmtpDeliveryMethod.Network, UseDefaultCredentials = false };
        if (!string.IsNullOrWhiteSpace(_options.UserName)) client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message);
        _logger.LogInformation("Sent email notification. EventId={EventId}, CustomerId={CustomerId}, OrderId={OrderId}", eventId, customerId, orderId);
    }
}