using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NotificationWorker.Application.Abstractions;
using NotificationWorker.Infrastructure.Identity;
using NotificationWorker.Infrastructure.Notifications;

namespace MicroShop.IntegrationTests.Notifications;

public sealed class SmtpNotificationSenderTests
{
    [Fact]
    public async Task VerifiedCustomerWhoOptedOut_DoesNotReceiveLifecycleEmail()
    {
        var customerId = Guid.NewGuid();
        var sender = new SmtpNotificationSender(
            new StaticContactClient(new CustomerContact(customerId, "customer@example.test", true, false)),
            Options.Create(new NotificationDeliveryOptions
            {
                Smtp = new SmtpOptions
                {
                    Host = "127.0.0.1",
                    Port = 1,
                    FromAddress = "no-reply@microshop.test",
                    PublicStorefrontBaseUrl = "https://shop.example.test"
                }
            }),
            NullLogger<SmtpNotificationSender>.Instance);

        await sender.SendOrderCreatedAsync(
            new OrderCreatedNotification(
                Guid.NewGuid(),
                Guid.NewGuid(),
                customerId,
                79m,
                "USD",
                DateTime.UtcNow,
                1,
                "test-correlation"),
            TestContext.Current.CancellationToken);
    }

    private sealed class StaticContactClient(CustomerContact contact) : ICustomerContactClient
    {
        public Task<CustomerContact?> GetAsync(Guid customerId, CancellationToken cancellationToken) =>
            Task.FromResult<CustomerContact?>(contact.CustomerId == customerId ? contact : null);
    }
}