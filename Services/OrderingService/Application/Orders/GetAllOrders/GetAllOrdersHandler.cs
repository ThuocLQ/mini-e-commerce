using MediatR;
using OrderingService.Application.Abstractions;

namespace OrderingService.Application.Orders.GetAllOrders;

public sealed class GetAllOrdersHandler(IOrderRepository repository)
    : IRequestHandler<GetAllOrdersQuery, IReadOnlyList<OrderDto>>
{
    public async Task<IReadOnlyList<OrderDto>> Handle(GetAllOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await repository.GetAllAsync(cancellationToken);
        return orders.Select(OrderMapper.ToDto).ToList();
    }
}