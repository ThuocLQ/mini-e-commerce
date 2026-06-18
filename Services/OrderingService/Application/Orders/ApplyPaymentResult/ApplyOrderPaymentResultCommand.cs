using MediatR;

namespace OrderingService.Application.Orders.ApplyPaymentResult;

public sealed record ApplyOrderPaymentResultCommand(
    Guid OrderId,
    OrderPaymentResult Result) : IRequest<OrderDto?>;

public enum OrderPaymentResult
{
    Succeeded = 1,
    Failed = 2
}
