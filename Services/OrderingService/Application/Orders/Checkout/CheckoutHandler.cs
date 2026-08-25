using MediatR;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using OrderingService.Application.Abstractions;
using OrderingService.Application.Baskets;
using OrderingService.Application.Inventory;
using OrderingService.Application.Discounts;
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
    private readonly IInventoryReservationClient _inventoryReservationClient;
    private readonly IOrderingUnitOfWork _unitOfWork;
    private readonly OrderEventOptions _eventOptions;
    private readonly ILogger<CheckoutHandler> _logger;

    public CheckoutHandler(
        IBasketClient basketClient,
        IOrderRepository orderRepository,
        IOutboxRepository outboxRepository,
        ICatalogProductSnapshotClient catalogProductClient,
        IDiscountClient discountClient,
        IInventoryReservationClient inventoryReservationClient,
        IOrderingUnitOfWork unitOfWork,
        IOptions<OrderEventOptions> eventOptions,
        ILogger<CheckoutHandler> logger)
    {
        _basketClient = basketClient;
        _orderRepository = orderRepository;
        _outboxRepository = outboxRepository;
        _catalogProductClient = catalogProductClient;
        _discountClient = discountClient;
        _inventoryReservationClient = inventoryReservationClient;
        _unitOfWork = unitOfWork;
        _eventOptions = eventOptions.Value;
        _logger = logger;
    }
    
    public async Task<OrderDto> Handle(CheckoutCommand request, CancellationToken cancellationToken)
    {
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        EnsureValidBasketId(request.BasketId);
        EnsureValidBasketVersion(request.BasketVersion);
        var existingOrder = await _orderRepository.GetByCustomerAndIdempotencyKeyAsync(
            request.CustomerId,
            idempotencyKey,
            cancellationToken);

        if (existingOrder is not null)
        {
            EnsureMatchingCoupon(existingOrder, request.CouponCode);
            EnsureMatchingBasketIdentity(existingOrder, request.BasketId, request.BasketVersion);
            return OrderMapper.ToDto(existingOrder);
        }

        var existingCheckout = await _orderRepository.GetByCustomerAndCheckoutBasketAsync(
            request.CustomerId,
            request.BasketId,
            request.BasketVersion,
            cancellationToken);

        if (existingCheckout is not null)
        {
            EnsureMatchingCoupon(existingCheckout, request.CouponCode);
            EnsureMatchingBasketIdentity(existingCheckout, request.BasketId, request.BasketVersion);
            return OrderMapper.ToDto(existingCheckout);
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

        if (basket.BasketId != request.BasketId || basket.Version != request.BasketVersion)
        {
            throw new CheckoutIdempotencyConflictException(
                "Basket changed before checkout. Refresh the basket and use a new idempotency key.");
        }

        var checkoutRequestHash = CreateCheckoutRequestHash(basket, request.CouponCode);

        var order = new Order(
            Guid.NewGuid(),
            request.CustomerId,
            DateTime.UtcNow,
            OrderStatus.PendingPayment,
            idempotencyKey,
            _eventOptions.Currency,
            checkoutRequestHash,
            basket.Version,
            basket.BasketId);

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
            var discount = await _discountClient.ReserveAsync(
                request.CouponCode.Trim(),
                order.Id,
                request.CustomerId,
                order.SubtotalAmount,
                DateTime.UtcNow.AddMinutes(30),
                cancellationToken);

            if (!discount.IsReserved || discount.ReservationId is null || discount.DiscountAmount <= 0 ||
                discount.FinalAmount != order.SubtotalAmount - discount.DiscountAmount)
            {
                throw new ArgumentException(discount.Message);
            }

            order.ApplyDiscount(discount.CouponCode, discount.DiscountAmount);
            order.AttachDiscountReservation(discount.ReservationId.Value);
        }

        var reservation = await _inventoryReservationClient.ReserveAsync(
            order.Id,
            order.Items.Select(item => new InventoryReservationItem(item.ProductId, item.Quantity)).ToList(),
            DateTime.UtcNow.AddMinutes(30),
            cancellationToken);
        if (!reservation.Succeeded)
        {
            await TryReleaseDiscountReservationAsync(order, "Inventory reservation was rejected.");
            throw new InsufficientInventoryException(reservation.FailureReason);
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
            await TryReleaseReservationAfterPersistenceFailureAsync(order.Id);
            await TryReleaseDiscountReservationAsync(order, "Order persistence found a duplicate checkout.");

            var duplicatedOrder = await _orderRepository.GetByCustomerAndIdempotencyKeyAsync(
                request.CustomerId,
                idempotencyKey,
                cancellationToken)
                ?? await _orderRepository.GetByCustomerAndCheckoutBasketAsync(
                    request.CustomerId,
                    request.BasketId,
                    request.BasketVersion,
                    cancellationToken);

            if (duplicatedOrder is null)
            {
                throw;
            }

            EnsureMatchingCoupon(duplicatedOrder, request.CouponCode);
            EnsureMatchingBasketIdentity(duplicatedOrder, request.BasketId, request.BasketVersion);
            return OrderMapper.ToDto(duplicatedOrder);
        }
        catch
        {
            await TryReleaseReservationAfterPersistenceFailureAsync(order.Id);
            await TryReleaseDiscountReservationAsync(order, "Order persistence failed.");

            throw;
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

    private static string CreateCheckoutRequestHash(BasketDto basket, string? couponCode)
    {
        var normalizedCouponCode = string.IsNullOrWhiteSpace(couponCode)
            ? string.Empty
            : couponCode.Trim().ToUpperInvariant();

        var canonicalItems = basket.Items!
            .Select(item => new
            {
                ProductId = string.IsNullOrWhiteSpace(item.ProductId)
                    ? throw new ArgumentException("Basket contains an invalid product id.")
                    : item.ProductId.Trim().ToUpperInvariant(),
                item.Quantity
            })
            .GroupBy(item => item.ProductId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}:{group.Sum(item => item.Quantity)}");

        var canonicalRequest = $"basketId={basket.BasketId:D}\nbasketVersion={basket.Version}\ncoupon={normalizedCouponCode}\nitems={string.Join(',', canonicalItems)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void EnsureMatchingBasketIdentity(Order order, Guid basketId, long basketVersion)
    {
        if (string.IsNullOrWhiteSpace(order.CheckoutRequestHash) ||
            order.CheckoutBasketId != basketId ||
            order.CheckoutBasketVersion != basketVersion)
        {
            throw new CheckoutIdempotencyConflictException(
                "Idempotency key was already used for a different checkout request.");
        }
    }

    private static void EnsureMatchingCoupon(Order order, string? couponCode)
    {
        var requestedCouponCode = string.IsNullOrWhiteSpace(couponCode)
            ? null
            : couponCode.Trim().ToUpperInvariant();

        if (!string.Equals(order.DiscountCode, requestedCouponCode, StringComparison.Ordinal))
        {
            throw new CheckoutIdempotencyConflictException(
                "Idempotency key was already used with a different coupon code.");
        }
    }

    private static void EnsureValidBasketVersion(long basketVersion)
    {
        if (basketVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(basketVersion), "BasketVersion must be greater than zero.");
        }
    }

    private static void EnsureValidBasketId(Guid basketId)
    {
        if (basketId == Guid.Empty)
        {
            throw new ArgumentException("BasketId cannot be empty.", nameof(basketId));
        }
    }

    private async Task TryReleaseReservationAfterPersistenceFailureAsync(Guid orderId)
    {
        try
        {
            await _inventoryReservationClient.ReleaseAsync(orderId, CancellationToken.None);
        }
        catch (InventoryUnavailableException exception)
        {
            _logger.LogWarning(exception, "Could not release inventory after checkout persistence failed. OrderId: {OrderId}", orderId);
        }
    }

    private async Task TryReleaseDiscountReservationAsync(Order order, string reason)
    {
        if (order.DiscountReservationId is not { } reservationId)
        {
            return;
        }

        try
        {
            await _discountClient.ReleaseAsync(reservationId, order.Id, reason, CancellationToken.None);
        }
        catch (DiscountUnavailableException exception)
        {
            _logger.LogWarning(exception, "Could not release discount reservation after checkout failure. OrderId: {OrderId}", order.Id);
        }
    }
}
