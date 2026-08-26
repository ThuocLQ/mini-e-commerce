export type Order = {
  id: string;
  customerId: string;
  createdAtUtc: string;
  status: string;
  totalAmount: number;
  currency: string;
  discountCode: string | null;
  discountAmount: number;
  items: unknown[];
};

export type Payment = {
  id: string;
  orderId: string;
  customerId: string;
  amount: number;
  currency: string;
  status: string;
  providerTransactionId: string | null;
  failureReason: string | null;
  createdAtUtc: string;
  completedAtUtc: string | null;
};

export type OrderPaymentRow = Order & { payment: Payment | null };

export async function loadOrderPaymentQueue(signal?: AbortSignal): Promise<OrderPaymentRow[]> {
  const [orderResponse, paymentResponse] = await Promise.all([
    fetch("/api/orders/admin", { signal }),
    fetch("/api/payments/admin?limit=200", { signal }),
  ]);
  const [orders, payments] = await Promise.all([
    orderResponse.json().catch(() => null),
    paymentResponse.json().catch(() => null),
  ]);

  if (!orderResponse.ok || !isOrderList(orders)) {
    throw new OperationsApiError(orderResponse.status, messageOf(orders) ?? "Orders could not be loaded.");
  }
  if (!paymentResponse.ok || !isPaymentList(payments)) {
    throw new OperationsApiError(paymentResponse.status, messageOf(payments) ?? "Payments could not be loaded.");
  }

  const paymentByOrderId = new Map<string, Payment>();
  for (const payment of payments) {
    const current = paymentByOrderId.get(payment.orderId);
    if (!current || payment.createdAtUtc > current.createdAtUtc) paymentByOrderId.set(payment.orderId, payment);
  }
  return orders.map((order) => ({ ...order, payment: paymentByOrderId.get(order.id) ?? null }));
}

export class OperationsApiError extends Error {
  constructor(readonly status: number, message: string) {
    super(message);
  }
}

function isOrderList(value: unknown): value is Order[] {
  return Array.isArray(value) && value.every((item) => isRecord(item)
    && typeof item.id === "string"
    && typeof item.customerId === "string"
    && typeof item.createdAtUtc === "string"
    && typeof item.status === "string"
    && typeof item.totalAmount === "number"
    && typeof item.currency === "string"
    && (item.discountCode === null || typeof item.discountCode === "string")
    && typeof item.discountAmount === "number"
    && Array.isArray(item.items));
}

function isPaymentList(value: unknown): value is Payment[] {
  return Array.isArray(value) && value.every((item) => isRecord(item)
    && typeof item.id === "string"
    && typeof item.orderId === "string"
    && typeof item.customerId === "string"
    && typeof item.amount === "number"
    && typeof item.currency === "string"
    && typeof item.status === "string"
    && (item.providerTransactionId === null || typeof item.providerTransactionId === "string")
    && (item.failureReason === null || typeof item.failureReason === "string")
    && typeof item.createdAtUtc === "string"
    && (item.completedAtUtc === null || typeof item.completedAtUtc === "string"));
}

function messageOf(value: unknown): string | null {
  return isRecord(value) && typeof value.message === "string" ? value.message : null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}
