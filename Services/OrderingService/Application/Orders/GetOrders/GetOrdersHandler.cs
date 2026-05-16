using MediatR;
using OrderingService.Application.Abstractions;

namespace OrderingService.Application.Orders.GetOrders;

public sealed class GetOrdersHandler : IRequestHandler<GetOrdersQuery, IReadOnlyList<OrderDto>>
{
    private readonly IOrderRepository _repository;

    public GetOrdersHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<OrderDto>> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await _repository.GetAllAsync(cancellationToken);

        return orders
            .Select(OrderMapper.ToDto)
            .ToList();
    }
}
