using MediatR;
using Microsoft.Extensions.Options;
using OrderingService.Application.Abstractions;
using OrderingService.Application.Baskets;
using OrderingService.Application.IntegrationEvents;
using OrderingService.Application.Orders;
using OrderingService.Application.Outbox;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Orders.Checkout;

public class CheckoutHandler : IRequestHandler<CheckoutCommand, OrderDto>
{
    private readonly IBasketClient _basketClient;
    private readonly IOrderRepository _orderRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly ICatalogProductSnapshotClient _catalogProductClient;
    private readonly IDiscountClient _discountClient;
    private readonly IOrderingUnitOfWork _unitOfWork;
    private readonly OrderEventOptions _eventOptions;
    private readonly ILogger<CheckoutHandler> _logger;

    public CheckoutHandler(
        IBasketClient basketClient,
        IOrderRepository orderRepository,
        IOutboxRepository outboxRepository,
        ICatalogProductSnapshotClient catalogProductClient,
        IDiscountClient discountClient,
        IOrderingUnitOfWork unitOfWork,
        IOptions<OrderEventOptions> eventOptions,
        ILogger<CheckoutHandler> logger)
    {
        _basketClient = basketClient;
        _orderRepository = orderRepository;
        _outboxRepository = outboxRepository;
        _catalogProductClient = catalogProductClient;
        _discountClient = discountClient;
        _unitOfWork = unitOfWork;
        _eventOptions = eventOptions.Value;
        _logger = logger;
    }
    
    public async Task<OrderDto> Handle(CheckoutCommand request, CancellationToken cancellationToken)
    {
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        var existingOrder = await _orderRepository.GetByCustomerAndIdempotencyKeyAsync(
            request.CustomerId,
            idempotencyKey,
            cancellationToken);

        if (existingOrder is not null)
        {
            return OrderMapper.ToDto(existingOrder);
        }

        var basket = await _basketClient.GetBasketAsync(request.CustomerId, cancellationToken);

        if (basket is null || basket.Items is null || basket.Items.Count == 0)
        {
            throw new InvalidOperationException("Basket is empty.");
        }

        if (!string.Equals(basket.UserId, request.CustomerId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Basket does not belong to the requested customer.");
        }

        var order = new Order(
            Guid.NewGuid(),
            request.CustomerId,
            DateTime.UtcNow,
            OrderStatus.PendingPayment,
            idempotencyKey,
            _eventOptions.Currency);

        foreach (var item in basket.Items)
        {
            if (!Guid.TryParse(item.ProductId, out var productId) || productId == Guid.Empty)
            {
                throw new ArgumentException("Basket contains an invalid product id.");
            }

            var product = await _catalogProductClient.GetProductAsync(productId, cancellationToken);
            if (product is null)
            {
                throw new ArgumentException($"Product '{productId:D}' no longer exists and cannot be checked out.");
            }

            if (product.Price < 0)
            {
                throw new InvalidOperationException($"Product '{productId:D}' has an invalid current price.");
            }

            order.AddItem(new OrderItem(
                Guid.NewGuid(),
                productId,
                product.Name,
                product.Price,
                item.Quantity));
        }

        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var discount = await _discountClient.ApplyAsync(
                request.CouponCode.Trim(),
                order.SubtotalAmount,
                cancellationToken);

            if (!discount.IsValid || discount.DiscountAmount <= 0 ||
                discount.FinalAmount != order.SubtotalAmount - discount.DiscountAmount)
            {
                throw new ArgumentException(discount.Message);
            }

            order.ApplyDiscount(discount.CouponCode, discount.DiscountAmount);
        }

        Order createdOrder;
        try
        {
            createdOrder = await _unitOfWork.ExecuteAsync(async transaction =>
            {
                var persistedOrder = await _orderRepository.CreateAsync(order, transaction, cancellationToken);
                var orderCreatedEvent = OrderIntegrationEventFactory.CreateOrderCreated(persistedOrder);
                var notificationOutboxMessage = OutboxMessageFactory.Create(orderCreatedEvent);
                var projectionEvent = OrderIntegrationEventFactory.CreateOrderProjectionCreated(persistedOrder);
                var projectionOutboxMessage = OutboxMessageFactory.CreateKafka(projectionEvent);

                await _outboxRepository.AddAsync(notificationOutboxMessage, transaction, cancellationToken);
                await _outboxRepository.AddAsync(projectionOutboxMessage, transaction, cancellationToken);

                return persistedOrder;
            }, cancellationToken);
        }
        catch (OrderAlreadyExistsException)
        {
            var duplicatedOrder = await _orderRepository.GetByCustomerAndIdempotencyKeyAsync(
                request.CustomerId,
                idempotencyKey,
                cancellationToken);

            if (duplicatedOrder is null)
            {
                throw;
            }

            return OrderMapper.ToDto(duplicatedOrder);
        }

        try
        {
            var cleared = await _basketClient.TryClearBasketAsync(request.CustomerId, basket.Version, cancellationToken);
            if (!cleared)
            {
                _logger.LogInformation(
                    "Basket was retained after checkout because its version changed. OrderId: {OrderId}, CustomerId: {CustomerId}, BasketVersion: {BasketVersion}",
                    createdOrder.Id,
                    request.CustomerId,
                    basket.Version);
            }
        }
        catch (BasketUnavailableException exception)
        {
            _logger.LogWarning(
                exception,
                "Order was created but BasketService was unavailable for conditional clear. OrderId: {OrderId}, CustomerId: {CustomerId}",
                createdOrder.Id,
                request.CustomerId);
        }

        return OrderMapper.ToDto(createdOrder);
    }

    private static string NormalizeIdempotencyKey(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency-Key header or idempotencyKey body field is required.");
        }

        idempotencyKey = idempotencyKey.Trim();
        if (idempotencyKey.Length > 128)
        {
            throw new ArgumentException("Idempotency key cannot exceed 128 characters.");
        }

        return idempotencyKey;
    }
}
