using MediatR;
using Microsoft.Extensions.Options;
using OrderingService.Application.Abstractions;
using OrderingService.Application.IntegrationEvents;
using OrderingService.Application.Outbox;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Orders.CreateOrder;

public sealed class CreateOrderHandler : IRequestHandler<CreateOrderCommand, OrderDto>
{
    private readonly IOrderRepository _repository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IOrderingUnitOfWork _unitOfWork;
    private readonly OrderEventOptions _eventOptions;

    public CreateOrderHandler(
        IOrderRepository repository,
        IOutboxRepository outboxRepository,
        IOrderingUnitOfWork unitOfWork,
        IOptions<OrderEventOptions> eventOptions)
    {
        _repository = repository;
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
        _eventOptions = eventOptions.Value;
    }

    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Order must have at least one item.");
        }

        var order = new Order(
            Guid.NewGuid(),
            request.CustomerId,
            DateTime.UtcNow,
            OrderStatus.PendingPayment);

        foreach (var item in request.Items)
        {
            order.AddItem(new OrderItem(
                Guid.NewGuid(),
                item.ProductId,
                item.ProductName,
                item.UnitPrice,
                item.Quantity));
        }

        var createdOrder = await _unitOfWork.ExecuteAsync(async transaction =>
        {
            var persistedOrder = await _repository.CreateAsync(order, transaction, cancellationToken);
            var orderCreatedEvent = OrderIntegrationEventFactory.CreateOrderCreated(persistedOrder, _eventOptions.Currency);
            var outboxMessage = OutboxMessageFactory.Create(orderCreatedEvent);

            await _outboxRepository.AddAsync(outboxMessage, transaction, cancellationToken);

            return persistedOrder;
        }, cancellationToken);

        return OrderMapper.ToDto(createdOrder);
    }
}
