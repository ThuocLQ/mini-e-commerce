using MediatR;

namespace OrderingService.Application.OrderPaymentSagas.ApplyInventorySettlement;

public sealed record ApplyInventorySettlementEventCommand(
    Guid EventId,
    OrderInventorySettlementEventType EventType,
    Guid OrderId,
    string? Reason,
    Guid? CausationEventId = null) : IRequest<InventorySettlementApplyResult>;

public enum OrderInventorySettlementEventType
{
    InventoryCommitted = 1,
    InventoryReleased = 2
}
