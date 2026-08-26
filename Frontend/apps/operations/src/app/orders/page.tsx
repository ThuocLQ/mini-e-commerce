"use client";

import Link from "next/link";
import { ArrowLeft, CircleAlert, ClipboardList, LoaderCircle, RefreshCw } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";

type Order = { id: string; customerId: string; createdAtUtc: string; status: string; totalAmount: number; currency: string; discountCode: string | null; discountAmount: number; items: unknown[] };
const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

export default function OrdersPage() {
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const getOrders = useCallback(async () => {
    const response = await fetch("/api/orders/admin");
    const payload = await response.json().catch(() => null);

    if (!response.ok || !Array.isArray(payload)) {
      throw new Error(messageOf(payload) ?? "Orders could not be loaded.");
    }

    return payload as Order[];
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      setOrders(await getOrders());
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Orders could not be loaded.");
    } finally {
      setLoading(false);
    }
  }, [getOrders]);

  useEffect(() => {
    let active = true;

    async function loadInitialOrders() {
      try {
        const result = await getOrders();
        if (active) setOrders(result);
      } catch (exception) {
        if (active) setError(exception instanceof Error ? exception.message : "Orders could not be loaded.");
      } finally {
        if (active) setLoading(false);
      }
    }

    void loadInitialOrders();
    return () => { active = false; };
  }, [getOrders]);

  const summary = useMemo(() => ({
    pending: orders.filter(order => order.status === "PendingPayment").length,
    paid: orders.filter(order => order.status === "Paid").length,
    paidRevenue: orders.filter(order => order.status === "Paid").reduce((sum, order) => sum + order.totalAmount, 0)
  }), [orders]);

  return <main className="orders-page"><header className="orders-header"><Link className="back" href="/"><ArrowLeft size={17} />Catalog control</Link><div className="orders-title"><div><p className="eyebrow">Operations</p><h1>Order queue</h1></div><button className="icon-button" aria-label="Refresh orders" title="Refresh orders" disabled={loading} onClick={() => void load()}>{loading ? <LoaderCircle className="spin" size={18} /> : <RefreshCw size={18} />}</button></div></header><div className="metrics"><Metric label="Orders" value={orders.length.toString()} /><Metric label="Pending payment" value={summary.pending.toString()} tone="warn" /><Metric label="Paid revenue" value={money.format(summary.paidRevenue)} /></div>{error ? <div className="notice"><CircleAlert size={19} /><span>{error}</span><button onClick={() => void load()}>Retry</button></div> : null}<div className="table-wrap"><table><thead><tr><th>Order</th><th>Customer</th><th>Created</th><th>Status</th><th>Items</th><th>Total</th></tr></thead><tbody>{orders.map(order => <tr key={order.id}><td><strong><ClipboardList size={16} />{shortId(order.id)}</strong><span>{order.discountCode ? `${order.discountCode} applied` : "No promotion"}</span></td><td><code>{shortId(order.customerId)}</code></td><td>{new Date(order.createdAtUtc).toLocaleString()}</td><td><Status value={order.status} /></td><td>{order.items.length}</td><td><strong>{money.format(order.totalAmount)}</strong><span>{order.currency}</span></td></tr>)}</tbody></table>{!loading && orders.length === 0 ? <div className="empty">No orders have been created yet.</div> : null}</div></main>;
}

function Metric({ label, value, tone }: { label: string; value: string; tone?: string }) { return <div className={`metric ${tone ?? ""}`}><span>{label}</span><strong>{value}</strong></div>; }
function Status({ value }: { value: string }) { const className = value === "Paid" ? "status paid" : value === "Cancelled" ? "status cancelled" : "status pending"; return <span className={className}>{value.replace(/([A-Z])/g, " $1").trim()}</span>; }
function shortId(value: string) { return value.slice(0, 8); }
function messageOf(value: unknown): string | null { return typeof value === "object" && value !== null && typeof (value as Record<string, unknown>).message === "string" ? (value as { message: string }).message : null; }
