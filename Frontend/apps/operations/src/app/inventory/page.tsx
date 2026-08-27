"use client";

import Link from "next/link";
import { ArrowLeft, Boxes, CircleAlert, LoaderCircle, RefreshCw, ShieldAlert } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { OperationsWorkspace } from "@/components/operations-workspace";
import { loadInventoryReconciliation, OperationsApiError, type InventoryReconciliation, type ReconciliationState } from "@/lib/operations/inventory-reconciliation";

type User = { userId: string; role: string };
type Filter = "attention" | "all" | "balanced" | "low";
const dateTime = new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeStyle: "short" });
const emptyData: InventoryReconciliation = { rows: [], productsMissingInventory: [] };

export default function InventoryPage() {
  const [authorized, setAuthorized] = useState<boolean | null>(null);
  const [data, setData] = useState<InventoryReconciliation>(emptyData);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<Filter>("attention");
  const [updatedAt, setUpdatedAt] = useState<Date | null>(null);

  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try { setData(await loadInventoryReconciliation()); setUpdatedAt(new Date()); }
    catch (exception) {
      if (exception instanceof OperationsApiError && (exception.status === 401 || exception.status === 403)) { window.location.replace("/"); return; }
      setError(exception instanceof Error ? exception.message : "Inventory could not be loaded.");
    } finally { setLoading(false); }
  }, []);

  useEffect(() => {
    let active = true;
    const controller = new AbortController();
    async function initialize() {
      try {
        const response = await fetch("/api/session", { signal: controller.signal });
        const payload = response.ok ? await response.json().catch(() => null) : null;
        if (!active) return;
        if (!isUser(payload?.user) || payload.user.role !== "Admin") { window.location.replace("/"); return; }
        setAuthorized(true);
        await load();
      } catch (exception) {
        if (!active || isAbortError(exception)) return;
        setAuthorized(true); setError("Your session could not be checked. Refresh to try again.");
      }
    }
    void initialize();
    return () => { active = false; controller.abort(); };
  }, [load]);

  const summary = useMemo(() => ({
    available: data.rows.reduce((sum, item) => sum + item.availableQuantity, 0),
    attention: data.rows.filter((item) => item.reconciliation !== "balanced").length,
    missingInventory: data.productsMissingInventory.length,
  }), [data]);
  const visibleRows = useMemo(() => data.rows.filter((item) => {
    if (filter === "attention") return item.reconciliation !== "balanced";
    if (filter === "balanced") return item.reconciliation === "balanced";
    if (filter === "low") return item.availableQuantity >= 0 && item.availableQuantity < 10;
    return true;
  }), [data.rows, filter]);

  if (authorized !== true) return <main className="signin"><div><LoaderCircle className="spin" size={22} /><p>Checking administrator access…</p></div></main>;

  return <OperationsWorkspace area="inventory"><main className="orders-page">
    <header className="orders-header"><Link className="back" href="/"><ArrowLeft size={17} />Catalog control</Link><div className="orders-title"><div><p className="eyebrow">Operations / P0</p><h1>Inventory reconciliation</h1><p className="page-summary">Compare on-hand, reserved, and available units against the catalog records returned by the gateway.</p></div><button className="icon-button" aria-label="Refresh inventory reconciliation" title="Refresh inventory reconciliation" disabled={loading} onClick={() => void load()}>{loading ? <LoaderCircle className="spin" size={18} /> : <RefreshCw size={18} />}</button></div></header>
    <div className="metrics"><Metric label="Available units" value={String(summary.available)} /><Metric label="Quantity exceptions" value={String(summary.attention)} tone="danger" /><Metric label="Catalog missing inventory" value={String(summary.missingInventory)} tone="warn" /></div>
    {error ? <div className="notice"><CircleAlert size={19} /><span>{error}</span><button onClick={() => void load()}>Retry</button></div> : null}
    <div className="queue-toolbar"><div className="filter-row" aria-label="Inventory reconciliation filter">{(["attention", "all", "balanced", "low"] as Filter[]).map((value) => <button key={value} className={filter === value ? "filter-active" : ""} aria-pressed={filter === value} onClick={() => setFilter(value)}>{filterLabel(value)}</button>)}</div></div>
    <p className="queue-meta">{loading ? "Refreshing reconciliation…" : updatedAt ? `Last refreshed ${dateTime.format(updatedAt)} · ${visibleRows.length} records shown` : "Loading reconciliation…"}</p>
    <div className="table-wrap"><table><thead><tr><th>Product</th><th>On hand</th><th>Reserved</th><th>Available</th><th>Reconciliation</th><th>Last update</th></tr></thead><tbody>{visibleRows.map((item) => <tr key={item.productId}><td><strong><Boxes size={16} />{item.name}</strong><span>{item.description}</span></td><td>{item.stockQuantity}</td><td>{item.reservedQuantity}</td><td><Availability quantity={item.availableQuantity} /></td><td><ReconciliationStatus value={item.reconciliation} /></td><td>{formatDate(item.updatedAtUtc)}</td></tr>)}</tbody></table>{!loading && !error && visibleRows.length === 0 ? <div className="empty"><ShieldAlert size={22} /><p>{emptyMessage(data.rows.length, filter)}</p>{data.rows.length > 0 ? <button className="command" onClick={() => setFilter("all")}>Show all records</button> : null}</div> : null}</div>
    <section className="operations-section"><div className="section-heading"><div><p className="eyebrow">Coverage</p><h2>Catalog items without an inventory record</h2></div><p>This is a gateway snapshot, not a stock adjustment control.</p></div><div className="table-wrap"><table><thead><tr><th>Product</th><th>Product ID</th></tr></thead><tbody>{data.productsMissingInventory.map((product) => <tr key={product.id}><td><strong>{product.name}</strong><span>{product.description || "No description"}</span></td><td><code>{product.id}</code></td></tr>)}</tbody></table>{!loading && !error && data.productsMissingInventory.length === 0 ? <div className="empty">Every catalog product in this snapshot has an inventory record.</div> : null}</div></section>
  </main></OperationsWorkspace>;
}

function Metric({ label, value, tone }: { label: string; value: string; tone?: string }) { return <div className={`metric ${tone ?? ""}`}><span>{label}</span><strong>{value}</strong></div>; }
function Availability({ quantity }: { quantity: number }) { const className = quantity === 0 ? "stock out" : quantity < 10 ? "stock low" : "stock"; return <span className={className}>{quantity === 0 ? "Out of stock" : `${quantity} available`}</span>; }
function ReconciliationStatus({ value }: { value: ReconciliationState }) { const detail: Record<ReconciliationState, { label: string; className: string }> = { balanced: { label: "Balanced", className: "paid" }, quantityMismatch: { label: "Available does not reconcile", className: "cancelled" }, invalidQuantity: { label: "Invalid stock quantity", className: "cancelled" }, missingCatalog: { label: "Catalog item not found", className: "pending" } }; return <span className={`status ${detail[value].className}`}>{detail[value].label}</span>; }
function filterLabel(value: Filter) { return value === "attention" ? "Exceptions" : value === "all" ? "All records" : value === "balanced" ? "Balanced" : "Low availability"; }
function emptyMessage(rowCount: number, filter: Filter) { if (rowCount === 0) return "No inventory records are available yet."; if (filter === "attention") return "No quantity or catalog reconciliation exceptions were returned."; return "No inventory records match this filter."; }
function formatDate(value: string) { const date = new Date(value); return Number.isNaN(date.valueOf()) ? "Unknown" : dateTime.format(date); }
function isUser(value: unknown): value is User { return typeof value === "object" && value !== null && typeof (value as Record<string, unknown>).userId === "string" && typeof (value as Record<string, unknown>).role === "string"; }
function isAbortError(value: unknown) { return value instanceof DOMException && value.name === "AbortError"; }
