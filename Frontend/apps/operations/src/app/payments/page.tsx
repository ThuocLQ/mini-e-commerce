"use client";

import Link from "next/link";
import { ArrowLeft, BadgeDollarSign, CircleAlert, LoaderCircle, RefreshCw } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { OperationsWorkspace } from "@/components/operations-workspace";

type Payment = { id: string; orderId: string; customerId: string; amount: number; currency: string; status: string; providerTransactionId: string | null; failureReason: string | null; createdAtUtc: string; completedAtUtc: string | null };
const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

export default function PaymentsPage() {
  const [payments, setPayments] = useState<Payment[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const getPayments = useCallback(async () => {
    const response = await fetch("/api/payments/admin?limit=100");
    const payload = await response.json().catch(() => null);
    if (!response.ok || !Array.isArray(payload)) throw new Error(messageOf(payload) ?? "Payments could not be loaded.");
    return payload as Payment[];
  }, []);
  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try { setPayments(await getPayments()); }
    catch (exception) { setError(exception instanceof Error ? exception.message : "Payments could not be loaded."); }
    finally { setLoading(false); }
  }, [getPayments]);
  useEffect(() => {
    let active = true;
    async function loadInitialPayments() {
      try { const result = await getPayments(); if (active) setPayments(result); }
      catch (exception) { if (active) setError(exception instanceof Error ? exception.message : "Payments could not be loaded."); }
      finally { if (active) setLoading(false); }
    }
    void loadInitialPayments();
    return () => { active = false; };
  }, [getPayments]);
  const summary = useMemo(() => ({
    awaiting: payments.filter(payment => payment.status === "PendingAuthorization" || payment.status === "Authorized" || payment.status === "CapturePending").length,
    completed: payments.filter(payment => payment.status === "Captured" || payment.status === "Refunded" || payment.status === "Voided").length,
    capturedValue: payments.filter(payment => payment.status === "Captured").reduce((sum, payment) => sum + payment.amount, 0),
  }), [payments]);
  return <OperationsWorkspace area="payments"><main className="orders-page"><header className="orders-header"><Link className="back" href="/"><ArrowLeft size={17} />Catalog control</Link><div className="orders-title"><div><p className="eyebrow">Operations</p><h1>Payment ledger</h1></div><button className="icon-button" aria-label="Refresh payments" title="Refresh payments" disabled={loading} onClick={() => void load()}>{loading ? <LoaderCircle className="spin" size={18} /> : <RefreshCw size={18} />}</button></div></header><div className="metrics"><Metric label="Payments" value={payments.length.toString()} /><Metric label="Awaiting provider" value={summary.awaiting.toString()} tone="warn" /><Metric label="Captured value" value={money.format(summary.capturedValue)} /></div>{error ? <div className="notice"><CircleAlert size={19} /><span>{error}</span><button onClick={() => void load()}>Retry</button></div> : null}<div className="table-wrap"><table><thead><tr><th>Payment</th><th>Order</th><th>Created</th><th>Status</th><th>Provider reference</th><th>Amount</th></tr></thead><tbody>{payments.map(payment => <tr key={payment.id}><td><strong><BadgeDollarSign size={16} />{shortId(payment.id)}</strong><span>{shortId(payment.customerId)}</span></td><td><code>{shortId(payment.orderId)}</code></td><td>{new Date(payment.createdAtUtc).toLocaleString()}</td><td><PaymentStatus value={payment.status} reason={payment.failureReason} /></td><td><code>{payment.providerTransactionId ?? "Not assigned"}</code></td><td><strong>{money.format(payment.amount)}</strong><span>{payment.currency}</span></td></tr>)}</tbody></table>{!loading && payments.length === 0 ? <div className="empty">No payments have been initiated yet.</div> : null}</div></main></OperationsWorkspace>;
}

function Metric({ label, value, tone }: { label: string; value: string; tone?: string }) { return <div className={`metric ${tone ?? ""}`}><span>{label}</span><strong>{value}</strong></div>; }
function PaymentStatus({ value, reason }: { value: string; reason: string | null }) { const lower = value.toLowerCase(); const className = lower.includes("captured") ? "status paid" : lower.includes("failed") ? "status cancelled" : lower.includes("refund") || lower.includes("void") ? "status neutral" : "status pending"; return <span className={className}>{value.replace(/([A-Z])/g, " $1").trim()}{reason ? `: ${reason}` : ""}</span>; }
function shortId(value: string) { return value.slice(0, 8); }
function messageOf(value: unknown): string | null { return typeof value === "object" && value !== null && typeof (value as Record<string, unknown>).message === "string" ? (value as { message: string }).message : null; }
