using MediatR;
using OrderingService.Application.Abstractions;
using OrderingService.Domain.Orders;

namespace OrderingService.Application.Orders.ApplyPaymentResult;

public sealed class ApplyOrderPaymentResultHandler : IRequestHandler<ApplyOrderPaymentResultCommand, OrderDto?>
{
    private readonly IOrderRepository _repository;

    public ApplyOrderPaymentResultHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<OrderDto?> Handle(ApplyOrderPaymentResultCommand request, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var previousStatus = order.Status;
        var changed = request.Result switch
        {
            OrderPaymentResult.Succeeded => order.MarkPaid(),
            OrderPaymentResult.Failed => order.MarkPaymentFailed(),
            _ => throw new InvalidOperationException($"Unsupported payment result '{request.Result}'.")
        };

        if (!changed)
        {
            return OrderMapper.ToDto(order);
        }

        var updated = await _repository.TryUpdateStatusAsync(
            order.Id,
            order.Status,
            [previousStatus],
            cancellationToken: cancellationToken);

        if (updated)
        {
            return OrderMapper.ToDto(order);
        }

        var currentOrder = await _repository.GetByIdAsync(order.Id, cancellationToken);
        if (currentOrder is null)
        {
            return null;
        }

        if (currentOrder.Status == order.Status ||
            request.Result == OrderPaymentResult.Failed && currentOrder.Status == OrderStatus.Paid)
        {
            return OrderMapper.ToDto(currentOrder);
        }

        throw new InvalidOperationException("Order status changed before the payment result was applied. Retry the operation.");
    }
}
