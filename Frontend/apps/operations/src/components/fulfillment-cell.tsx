"use client";

import { Check, ChevronDown, History, LoaderCircle, PackagePlus, RefreshCw, Truck } from "lucide-react";
import { useState } from "react";

type Order = { id: string; status: string };
type Shipment = { id: string; status: string; carrier: string | null; trackingNumber: string | null };
type HistoryEntry = { id: string; previousStatus: string | null; currentStatus: string; actorId: string; reason: string; occurredAtUtc: string };
type ShipmentDetail = { shipment: Shipment; history: HistoryEntry[] };

export function FulfillmentCell({ order, onUpdated }: { order: Order; onUpdated: () => void }) {
  const [shipment, setShipment] = useState<Shipment | null>(null);
  const [history, setHistory] = useState<HistoryEntry[]>([]);
  const [busy, setBusy] = useState(false);
  const [showDispatchForm, setShowDispatchForm] = useState(false);
  const [showHistory, setShowHistory] = useState(false);
  const [carrier, setCarrier] = useState("");
  const [trackingNumber, setTrackingNumber] = useState("");
  const [message, setMessage] = useState<string | null>(null);

  async function refreshHistory() {
    const response = await fetch(`/api/orders/admin/${encodeURIComponent(order.id)}/shipment`, { cache: "no-store" });
    const payload: unknown = await response.json().catch(() => null);
    if (!response.ok || !isShipmentDetail(payload)) throw new Error(problemMessage(payload) ?? "Shipment history could not be loaded.");
    setShipment(payload.shipment);
    setHistory(payload.history);
  }

  async function mutate(path: string, body?: unknown) {
    setBusy(true); setMessage(null);
    try {
      const response = await fetch(`/api/orders/admin/${encodeURIComponent(order.id)}/shipment${path}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: body ? JSON.stringify(body) : undefined });
      const payload: unknown = await response.json().catch(() => null);
      if (!response.ok || !isShipment(payload)) throw new Error(problemMessage(payload) ?? "Fulfillment action failed.");
      setShipment(payload); setShowDispatchForm(false); await refreshHistory(); onUpdated();
    } catch (error) { setMessage(error instanceof Error ? error.message : "Fulfillment action failed."); }
    finally { setBusy(false); }
  }

  async function toggleHistory() { setShowHistory(current => !current); if (!showHistory) { try { await refreshHistory(); } catch (error) { setMessage(error instanceof Error ? error.message : "Shipment history could not be loaded."); } } }
  const action = shipment?.status ?? (order.status === "Paid" ? "Paid" : order.status);

  return <div className="fulfillment-cell">
    {action === "Paid" ? <button className="command" disabled={busy} onClick={() => void mutate("")}><PackagePlus size={15} />{busy ? "Creating" : "Create shipment"}</button> : null}
    {!shipment && action !== "Paid" ? <button className="command" disabled={busy} onClick={() => void refreshHistory()}><RefreshCw size={15} />Load shipment</button> : null}
    {shipment?.status === "ReadyToShip" ? <div className="action-row">{showDispatchForm ? <><input aria-label="Carrier" placeholder="Carrier" value={carrier} onChange={event => setCarrier(event.target.value)} /><input aria-label="Tracking number" placeholder="Tracking number" value={trackingNumber} onChange={event => setTrackingNumber(event.target.value)} /><button className="command primary" disabled={busy || !carrier.trim() || !trackingNumber.trim()} onClick={() => void mutate("/dispatch", { carrier, trackingNumber })}>{busy ? <LoaderCircle className="spin" size={15} /> : <Truck size={15} />}Dispatch</button></> : <button className="command" onClick={() => setShowDispatchForm(true)}><Truck size={15} />Add tracking</button>}</div> : null}
    {shipment?.status === "Shipped" ? <button className="command" disabled={busy} onClick={() => void mutate("/deliver")}>{busy ? <LoaderCircle className="spin" size={15} /> : <Check size={15} />}Mark delivered</button> : null}
    {shipment ? <><button className="command history-button" type="button" onClick={() => void toggleHistory()}><History size={15} />Audit <ChevronDown size={14} /></button>{showHistory ? <ul className="shipment-history">{history.map(item => <li key={item.id}><strong>{item.currentStatus}</strong><span>{new Date(item.occurredAtUtc).toLocaleString()} Â· {item.reason}</span></li>)}</ul> : null}</> : null}
    {message ? <span className="inline-error">{message}</span> : null}
  </div>;
}

function isShipment(value: unknown): value is Shipment { return isRecord(value) && typeof value.id === "string" && typeof value.status === "string"; }
function isShipmentDetail(value: unknown): value is ShipmentDetail { return isRecord(value) && isShipment(value.shipment) && Array.isArray(value.history); }
function problemMessage(value: unknown): string | null { return isRecord(value) && typeof value.message === "string" ? value.message : null; }
function isRecord(value: unknown): value is Record<string, unknown> { return typeof value === "object" && value !== null; }