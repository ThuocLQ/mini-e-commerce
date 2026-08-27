using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Payments.Providers;
using PaymentService.Application.Payments.Webhooks;
using PaymentService.Domain.Payments;
using PaymentService.Infrastructure.Providers;

namespace MicroShop.IntegrationTests.Payment;

public sealed class SandboxPaymentProviderTests
{
    [Fact]
    public async Task ApprovedCompletion_IsDeterministicSignedAuthorization_NotAPaidOrderShortcut()
    {
        var provider = CreateProvider();
        var payment = CreateSandboxPayment(DateTime.UtcNow.AddMinutes(30));

        var first = await provider.CompleteAsync(payment, SandboxPaymentOutcome.Approve, TestContext.Current.CancellationToken);
        var duplicate = await provider.CompleteAsync(payment, SandboxPaymentOutcome.Approve, TestContext.Current.CancellationToken);
        var payload = JsonSerializer.Deserialize<PaymentWebhookPayload>(first.RawBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(payload);
        Assert.Equal("AUTHORIZED", payload.Status);
        Assert.Equal(first.RawBody, duplicate.RawBody);
        Assert.Equal(first.Signature, duplicate.Signature);
        Assert.Equal(PaymentStatus.PendingAuthorization, payment.Status);
        Assert.True(HasValidSignature(first, "sandbox-webhook-secret-for-tests"));
    }

    [Fact]
    public async Task DeclinedOrExpiredSandboxAction_EmitsSignedProviderOutcomeWithoutCardData()
    {
        var provider = CreateProvider();
        var expiredPayment = CreateSandboxPayment(DateTime.UtcNow.AddMinutes(-1));

        var declined = await provider.CompleteAsync(expiredPayment, SandboxPaymentOutcome.Decline, TestContext.Current.CancellationToken);
        var payload = JsonSerializer.Deserialize<PaymentWebhookPayload>(declined.RawBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(payload);
        Assert.Equal("FAILED", payload.Status);
        Assert.Contains("declined", payload.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("card", declined.RawBody, StringComparison.OrdinalIgnoreCase);
        Assert.True(HasValidSignature(declined, "sandbox-webhook-secret-for-tests"));
    }

    [Fact]
    public async Task SandboxCompletion_UsesTheSameSignatureValidatedWebhookProcessor()
    {
        var payment = CreateSandboxPayment(DateTime.UtcNow.AddMinutes(30));
        var repository = new RecordingWebhookRepository(payment);
        using var services = new ServiceCollection()
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

        var provider = CreateProvider();
        var sandboxWebhook = await provider.CompleteAsync(payment, SandboxPaymentOutcome.Approve, TestContext.Current.CancellationToken);
        var processor = new PaymentWebhookProcessor(
            services.GetRequiredService<IOptions<PaymentWebhookOptions>>(),
            services.GetRequiredService<ISender>());

        var result = await processor.ProcessAsync(sandboxWebhook.RawBody, sandboxWebhook.Signature, TestContext.Current.CancellationToken);

        Assert.NotNull(result.Payment);
        Assert.Equal("Authorized", result.Payment.Status);
        Assert.Equal("Verified", repository.SignatureStatus);
        Assert.Equal(PaymentStatus.Authorized, repository.AppliedStatus);
        Assert.Equal(payment.Id, repository.AppliedPaymentId);
    }

    private static SandboxPaymentProvider CreateProvider() => new(
        Options.Create(new PaymentProviderOptions { Provider = "Sandbox", SandboxActionExpiryMinutes = 30 }),
        Options.Create(new PaymentWebhookOptions { SharedSecret = "sandbox-webhook-secret-for-tests", RequireSignature = true }));

    private static PaymentService.Domain.Payments.Payment CreateSandboxPayment(DateTime expiry) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 125_000m, "VND", PaymentStatus.PendingAuthorization, DateTime.UtcNow,
        provider: "Sandbox",
        providerSessionId: "sandbox-session-test",
        paymentActionIdempotencyKey: "sandbox-provider-test",
        paymentActionRequestHash: new string('a', 64),
        paymentActionExpiresAtUtc: expiry);

    private static bool HasValidSignature(PaymentProviderWebhook webhook, string secret)
    {
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(webhook.RawBody))).ToLowerInvariant();
        return string.Equals(webhook.Signature, $"sha256={expected}", StringComparison.Ordinal);
    }

    private sealed class RecordingWebhookRepository(PaymentService.Domain.Payments.Payment payment) : IPaymentWebhookRepository
    {
        public string? SignatureStatus { get; private set; }
        public PaymentStatus AppliedStatus { get; private set; }
        public Guid AppliedPaymentId { get; private set; }

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
            SignatureStatus = signatureStatus;
            AppliedStatus = status;
            AppliedPaymentId = paymentId;
            payment.MarkAuthorized(providerTransactionId, receivedAtUtc);
            return Task.FromResult(new PaymentWebhookApplyResult(payment, false, providerEventId, status));
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
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoopPaymentMetrics : IPaymentMetrics
    {
        public void RecordWebhookRequest(string outcome) { }
    }
}
