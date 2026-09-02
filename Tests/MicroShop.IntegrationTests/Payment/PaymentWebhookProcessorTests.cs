using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Payments.Webhooks;
using PaymentService.Domain.Payments;

namespace MicroShop.IntegrationTests.Payment;

public sealed class PaymentWebhookProcessorTests
{
    [Fact]
    public async Task InvalidSignature_IsRejectedAndRecordedBeforeAnyPaymentApply()
    {
        var payment = CreatePayment();
        var repository = new RecordingWebhookRepository(payment);
        using var services = CreateServices(repository);
        var processor = CreateProcessor(services);

        var body = Payload(payment, "evt-invalid-signature");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => processor.ProcessAsync(
            body,
            "sha256=invalid",
            TestContext.Current.CancellationToken));

        Assert.Equal(1, repository.RejectedCount);
        Assert.Equal(0, repository.ApplyCount);
    }

    [Fact]
    public async Task DuplicateProviderEvent_IsReportedAsDuplicateWithoutASecondApply()
    {
        var payment = CreatePayment();
        var repository = new RecordingWebhookRepository(payment);
        using var services = CreateServices(repository);
        var processor = CreateProcessor(services);
        var payload = Payload(payment, "evt-duplicate");
        var signature = PaymentWebhookSignature.Create(payload, "sandbox-webhook-secret-for-tests");

        var first = await processor.ProcessAsync(payload, signature, TestContext.Current.CancellationToken);
        var duplicate = await processor.ProcessAsync(payload, signature, TestContext.Current.CancellationToken);

        Assert.NotNull(first.Payment);
        Assert.False(first.IsDuplicate);
        Assert.NotNull(duplicate.Payment);
        Assert.True(duplicate.IsDuplicate);
        Assert.Equal(1, repository.ApplyCount);
    }

    private static ServiceProvider CreateServices(RecordingWebhookRepository repository) => new ServiceCollection()
        .AddLogging()
        .AddSingleton<IOptions<PaymentWebhookOptions>>(Options.Create(new PaymentWebhookOptions
        {
            SharedSecret = "sandbox-webhook-secret-for-tests",
            RequireSignature = true
        }))
        .AddSingleton<IPaymentWebhookRepository>(repository)
        .AddSingleton<IPaymentMetrics, NoopPaymentMetrics>()
        .AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(PaymentWebhookHandler).Assembly))
        .BuildServiceProvider();

    private static PaymentWebhookProcessor CreateProcessor(ServiceProvider services) => new(
        services.GetRequiredService<IOptions<PaymentWebhookOptions>>(),
        services.GetRequiredService<ISender>());

    private static PaymentService.Domain.Payments.Payment CreatePayment() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 125_000m, "VND", PaymentStatus.PendingAuthorization, DateTime.UtcNow);

    private static string Payload(PaymentService.Domain.Payments.Payment payment, string eventId) =>
        $"{{\"paymentId\":\"{payment.Id}\",\"providerEventId\":\"{eventId}\",\"providerTransactionId\":\"provider-transaction-001\",\"status\":\"AUTHORIZED\",\"failureReason\":null}}";

    private sealed class RecordingWebhookRepository(PaymentService.Domain.Payments.Payment payment) : IPaymentWebhookRepository
    {
        private readonly HashSet<string> _eventIds = [];

        public int ApplyCount { get; private set; }
        public int RejectedCount { get; private set; }

        public Task<PaymentWebhookApplyResult> ApplyAsync(
            string providerEventId,
            Guid paymentId,
            string providerTransactionId,
            PaymentStatus status,
            string? failureReason,
            string payloadHash,
            string signatureStatus,
            DateTime receivedAtUtc,
            CancellationToken cancellationToken = default)
        {
            var isDuplicate = !_eventIds.Add(providerEventId);
            if (!isDuplicate)
            {
                ApplyCount++;
                payment.MarkAuthorized(providerTransactionId, receivedAtUtc);
            }

            return Task.FromResult(new PaymentWebhookApplyResult(payment, isDuplicate, providerEventId, status));
        }

        public Task RecordRejectedAsync(
            string providerEventId,
            Guid paymentId,
            string providerTransactionId,
            string eventType,
            string payloadHash,
            string signatureStatus,
            string error,
            DateTime receivedAtUtc,
            CancellationToken cancellationToken = default)
        {
            RejectedCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class NoopPaymentMetrics : IPaymentMetrics
    {
        public void RecordWebhookRequest(string outcome) { }
    }

    private static class PaymentWebhookSignature
    {
        public static string Create(string body, string secret)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
            return "sha256=" + Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        }
    }
}