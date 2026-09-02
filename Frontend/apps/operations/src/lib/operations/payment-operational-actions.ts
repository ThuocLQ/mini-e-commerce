export type PaymentOperationalAction = {
  id: string;
  paymentId: string;
  actionType: string;
  requestedBy: string;
  reason: string;
  requestedAtUtc: string;
  completedAtUtc: string | null;
  failureReason: string | null;
};

export async function loadPaymentOperationalActions(paymentId: string, signal?: AbortSignal): Promise<PaymentOperationalAction[]> {
  const response = await fetch(`/api/payments/admin/${encodeURIComponent(paymentId)}/actions`, { signal, cache: "no-store" });
  const payload: unknown = await response.json().catch(() => null);
  if (!response.ok || !isActionList(payload)) {
    throw new Error(messageOf(payload) ?? "Payment audit actions could not be loaded.");
  }

  return payload;
}

function isActionList(value: unknown): value is PaymentOperationalAction[] {
  return Array.isArray(value) && value.every((item) => isRecord(item)
    && typeof item.id === "string"
    && typeof item.paymentId === "string"
    && typeof item.actionType === "string"
    && typeof item.requestedBy === "string"
    && typeof item.reason === "string"
    && typeof item.requestedAtUtc === "string"
    && (item.completedAtUtc === null || typeof item.completedAtUtc === "string")
    && (item.failureReason === null || typeof item.failureReason === "string"));
}

function messageOf(value: unknown): string | null {
  return isRecord(value) && typeof value.message === "string" ? value.message : null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}