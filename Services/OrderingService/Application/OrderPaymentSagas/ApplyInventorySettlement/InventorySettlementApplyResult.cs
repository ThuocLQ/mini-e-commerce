namespace OrderingService.Application.OrderPaymentSagas.ApplyInventorySettlement;

public sealed record InventorySettlementApplyResult(
    bool OrderFound,
    OrderPaymentSagaDto? Saga);
