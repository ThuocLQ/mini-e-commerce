using MediatR;
using System.Security.Cryptography;
using System.Text;
using PaymentService.Application.Abstractions;
using PaymentService.Application.Payments.Providers;
using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.CreatePayment;

public sealed class CreatePaymentHandler : IRequestHandler<CreatePaymentCommand, CreatePaymentResult>
{
    private readonly IPaymentRepository _repository;
    private readonly IOrderPaymentClient _orderClient;
    private readonly IPaymentProvider _paymentProvider;

    public CreatePaymentHandler(
        IPaymentRepository repository,
        IOrderPaymentClient orderClient,
        IPaymentProvider paymentProvider)
    {
        _repository = repository;
        _orderClient = orderClient;
        _paymentProvider = paymentProvider;
    }

    public async Task<CreatePaymentResult> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        if (request.OrderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId is required.", nameof(request.OrderId));
        }

        if (request.CustomerId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Authenticated customer id is required.");
        }

        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);

        var order = await _orderClient.GetOrderAsync(request.OrderId, cancellationToken)
            ?? throw new PaymentOrderNotAccessibleException(request.OrderId);

        if (order.CustomerId != request.CustomerId)
        {
            throw new PaymentOrderNotAccessibleException(request.OrderId);
        }

        var requestHash = ComputeIntentHash(order.OrderId, order.CustomerId, order.TotalAmount, order.Currency);
        var replay = await _repository.GetByCustomerAndActionIdempotencyKeyAsync(
            request.CustomerId,
            idempotencyKey,
            cancellationToken);
        if (replay is not null)
        {
            EnsureSameIntent(replay, request.OrderId, requestHash, idempotencyKey);
            return ToResult(replay, isReplay: true);
        }

        var existingPayment = await _repository.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (existingPayment is not null)
        {
            EnsureSameIntent(existingPayment, request.OrderId, requestHash, idempotencyKey);
            return ToResult(existingPayment, isReplay: true);
        }

        if (!string.Equals(order.Status, "PendingPayment", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Payment can only be created for an order awaiting payment.");
        }

        var paymentId = Guid.NewGuid();
        var action = await _paymentProvider.CreateActionAsync(new PaymentProviderActionRequest(
            paymentId,
            order.OrderId,
            order.TotalAmount,
            order.Currency), cancellationToken);

        var payment = new Payment(
            paymentId,
            order.OrderId,
            order.CustomerId,
            order.TotalAmount,
            order.Currency,
            PaymentStatus.Pending,
            DateTime.UtcNow,
            provider: action.Provider,
            providerSessionId: action.SessionId,
            paymentActionIdempotencyKey: idempotencyKey,
            paymentActionRequestHash: requestHash,
            paymentActionExpiresAtUtc: action.ExpiresAtUtc);

        var createdPayment = await _repository.CreateAsync(payment, cancellationToken);

        EnsureSameIntent(createdPayment, request.OrderId, requestHash, idempotencyKey);
        return ToResult(createdPayment, isReplay: createdPayment.Id != payment.Id);
    }

    private static CreatePaymentResult ToResult(Payment payment, bool isReplay)
    {
        if (string.IsNullOrWhiteSpace(payment.Provider) ||
            string.IsNullOrWhiteSpace(payment.ProviderSessionId) ||
            payment.PaymentActionExpiresAtUtc is null)
        {
            throw new InvalidOperationException("Payment exists without a durable provider action. Create a new payment action with a new order.");
        }

        return new CreatePaymentResult(
            PaymentMapper.ToDto(payment),
            new PaymentActionDto(
                payment.Provider,
                payment.ProviderSessionId,
                payment.Status.ToString(),
                payment.PaymentActionExpiresAtUtc.Value,
                string.Equals(payment.Provider, "Sandbox", StringComparison.OrdinalIgnoreCase)),
            isReplay);
    }

    private static void EnsureSameIntent(Payment payment, Guid orderId, string requestHash, string idempotencyKey)
    {
        if (payment.OrderId != orderId ||
            !string.Equals(payment.PaymentActionIdempotencyKey, idempotencyKey, StringComparison.Ordinal) ||
            !string.Equals(payment.PaymentActionRequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new PaymentActionIdempotencyConflictException(
                "The idempotency key has already been used for a different payment action intent.");
        }
    }

    private static string NormalizeIdempotencyKey(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency-Key is required.", nameof(idempotencyKey));
        }

        var normalized = idempotencyKey.Trim();
        if (normalized.Length > 128)
        {
            throw new ArgumentException("Idempotency-Key cannot exceed 128 characters.", nameof(idempotencyKey));
        }

        return normalized;
    }

    private static string ComputeIntentHash(Guid orderId, Guid customerId, decimal amount, string currency)
    {
        var intent = $"{orderId:N}|{customerId:N}|{amount:0.00}|{currency.Trim().ToUpperInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(intent))).ToLowerInvariant();
    }
}
