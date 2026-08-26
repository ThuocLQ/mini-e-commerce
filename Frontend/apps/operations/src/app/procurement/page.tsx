"use client";

import Link from "next/link";
import { ArrowLeft, Building2, CircleAlert, ClipboardPlus, LoaderCircle, PackageCheck, Plus, Send, Trash2, Truck, X } from "lucide-react";
import { FormEvent, useCallback, useEffect, useMemo, useState } from "react";

type Supplier = { id: string; name: string; contactEmail: string | null; active: boolean };
type Product = { id: string; name: string; price: number };
type Line = { id: string; productId: string; productName: string; quantity: number; unitCost: number };
type PurchaseOrder = { id: string; number: string; supplierId: string; status: "DRAFT" | "SUBMITTED" | "RECEIPT_PENDING" | "RECEIVED" | "CANCELLED"; currency: string; lines: Line[]; createdAtUtc: string; submittedAtUtc: string | null; receiptId: string | null; receiptRequestedAtUtc: string | null; receivedAtUtc: string | null };
const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });
const dateTime = new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeStyle: "short" });

export default function ProcurementPage() {
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [orders, setOrders] = useState<PurchaseOrder[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<string | null>(null);
  const [supplierDialogOpen, setSupplierDialogOpen] = useState(false);
  const [orderDialogOpen, setOrderDialogOpen] = useState(false);
  const [submittingId, setSubmittingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true); setMessage(null);
    try {
      const [supplierResponse, orderResponse, productResponse] = await Promise.all([
        fetch("/api/suppliers"), fetch("/api/procurement/purchase-orders"), fetch("/api/catalog/products"),
      ]);
      const [supplierPayload, orderPayload, productPayload] = await Promise.all([
        supplierResponse.json().catch(() => null), orderResponse.json().catch(() => null), productResponse.json().catch(() => null),
      ]);
      if (!supplierResponse.ok || !Array.isArray(supplierPayload)) throw new Error(messageOf(supplierPayload) ?? "Suppliers could not be loaded.");
      if (!orderResponse.ok || !Array.isArray(orderPayload)) throw new Error(messageOf(orderPayload) ?? "Purchase orders could not be loaded.");
      if (!productResponse.ok || !Array.isArray(productPayload)) throw new Error(messageOf(productPayload) ?? "Catalog products could not be loaded.");
      setSuppliers(supplierPayload as Supplier[]); setOrders(orderPayload as PurchaseOrder[]); setProducts(productPayload as Product[]);
    } catch (error) { setMessage(error instanceof Error ? error.message : "Procurement workspace is unavailable."); }
    finally { setLoading(false); }
  }, []);

useEffect(() => {
    let active = true;
    async function loadInitialProcurement() {
      try { await load(); }
      finally { if (!active) return; }
    }
    void loadInitialProcurement();
    return () => { active = false; };
  }, [load]);
  const summary = useMemo(() => ({ suppliers: suppliers.length, drafts: orders.filter(order => order.status === "DRAFT").length, submitted: orders.filter(order => order.status === "SUBMITTED").length }), [suppliers, orders]);

  async function submitOrder(order: PurchaseOrder) {
    if (!window.confirm(`Submit ${order.number} to its supplier? Stock will not change until goods are received.`)) return;
    setSubmittingId(order.id); setMessage(null);
    try {
      const response = await fetch(`/api/procurement/purchase-orders/${encodeURIComponent(order.id)}/submit`, { method: "POST" });
      const payload = await response.json().catch(() => null);
      if (!response.ok) throw new Error(messageOf(payload) ?? "Purchase order could not be submitted.");
      await load();
    } catch (error) { setMessage(error instanceof Error ? error.message : "Purchase order could not be submitted."); }
    finally { setSubmittingId(null); }
  }

  async function receiveOrder(order: PurchaseOrder) {
    if (!window.confirm(`Confirm receipt for ${order.number}? This increases on-hand stock exactly once.`)) return;
    setSubmittingId(order.id); setMessage(null);
    try {
      const response = await fetch(`/api/procurement/purchase-orders/${encodeURIComponent(order.id)}/receive`, { method: "POST" });
      const payload = await response.json().catch(() => null);
      if (!response.ok) throw new Error(messageOf(payload) ?? "Goods receipt could not be confirmed.");
      await load();
    } catch (error) { setMessage(error instanceof Error ? error.message : "Goods receipt could not be confirmed."); }
    finally { setSubmittingId(null); }
  }

  return <main className="orders-page"><header className="orders-header"><Link className="back" href="/"><ArrowLeft size={17} />Catalog control</Link><div className="orders-title"><div><p className="eyebrow">Operations</p><h1>Supplier & procurement</h1></div><div className="action-row"><button className="command" onClick={() => setSupplierDialogOpen(true)}><Building2 size={17} />New supplier</button><button className="command primary" disabled={suppliers.length === 0 || products.length === 0} onClick={() => setOrderDialogOpen(true)}><ClipboardPlus size={17} />New purchase order</button></div></div></header><div className="metrics"><Metric label="Active suppliers" value={String(summary.suppliers)} /><Metric label="Draft orders" value={String(summary.drafts)} tone="warn" /><Metric label="Submitted orders" value={String(summary.submitted)} /></div>{message ? <div className="notice"><CircleAlert size={19} /><span>{message}</span><button onClick={() => void load()}>Retry</button></div> : null}<section className="operations-section"><div className="section-heading"><div><p className="eyebrow">Directory</p><h2>Suppliers</h2></div></div><div className="table-wrap"><table><thead><tr><th>Supplier</th><th>Contact</th><th>Status</th></tr></thead><tbody>{suppliers.map(supplier => <tr key={supplier.id}><td><strong><Building2 size={16} />{supplier.name}</strong></td><td>{supplier.contactEmail ?? "No contact email"}</td><td><span className="status paid">{supplier.active ? "Active" : "Inactive"}</span></td></tr>)}</tbody></table>{!loading && suppliers.length === 0 ? <div className="empty">Create a supplier before raising a purchase order.</div> : null}</div></section><section className="operations-section"><div className="section-heading"><div><p className="eyebrow">Procurement</p><h2>Purchase orders</h2></div><p>Confirm a receipt only when the supplier goods have physically arrived.</p></div><div className="table-wrap"><table><thead><tr><th>Order</th><th>Supplier</th><th>Lines</th><th>Total</th><th>Status</th><th>Created</th><th aria-label="Actions" /></tr></thead><tbody>{orders.map(order => <tr key={order.id}><td><strong><Truck size={16} />{order.number}</strong></td><td>{suppliers.find(supplier => supplier.id === order.supplierId)?.name ?? "Unknown supplier"}</td><td>{order.lines.reduce((total, line) => total + line.quantity, 0)} units / {order.lines.length} lines</td><td>{money.format(order.lines.reduce((total, line) => total + line.quantity * line.unitCost, 0))}</td><td><Status status={order.status} /></td><td>{dateTime.format(new Date(order.createdAtUtc))}</td><td>{order.status === "DRAFT" ? <button className="icon-button" aria-label={`Submit ${order.number}`} title="Submit purchase order" disabled={submittingId === order.id} onClick={() => void submitOrder(order)}>{submittingId === order.id ? <LoaderCircle className="spin" size={17} /> : <Send size={17} />}</button> : null}{order.status === "SUBMITTED" || order.status === "RECEIPT_PENDING" ? <button className="icon-button" aria-label={`Receive ${order.number}`} title={order.status === "RECEIPT_PENDING" ? "Retry goods receipt" : "Confirm goods receipt"} disabled={submittingId === order.id} onClick={() => void receiveOrder(order)}>{submittingId === order.id ? <LoaderCircle className="spin" size={17} /> : <PackageCheck size={17} />}</button> : null}</td></tr>)}</tbody></table>{!loading && orders.length === 0 ? <div className="empty">Purchase orders will appear here after a draft is created.</div> : null}</div></section>{supplierDialogOpen ? <SupplierDialog onClose={() => setSupplierDialogOpen(false)} onSaved={() => { setSupplierDialogOpen(false); void load(); }} /> : null}{orderDialogOpen ? <PurchaseOrderDialog products={products} suppliers={suppliers} onClose={() => setOrderDialogOpen(false)} onSaved={() => { setOrderDialogOpen(false); void load(); }} /> : null}</main>;
}

function SupplierDialog({ onClose, onSaved }: { onClose: () => void; onSaved: () => void }) {
  const [name, setName] = useState(""); const [email, setEmail] = useState(""); const [error, setError] = useState<string | null>(null); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent) { event.preventDefault(); setBusy(true); setError(null); try { const response = await fetch("/api/suppliers", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name, contactEmail: email || null }) }); const payload = await response.json().catch(() => null); if (!response.ok) throw new Error(messageOf(payload) ?? "Supplier could not be created."); onSaved(); } catch (exception) { setError(exception instanceof Error ? exception.message : "Supplier could not be created."); } finally { setBusy(false); } }
  return <div className="dialog-backdrop"><form className="dialog" onSubmit={submit}><div className="dialog-heading"><div><p className="eyebrow">Supplier directory</p><h2>New supplier</h2></div><button className="icon-button" aria-label="Close" onClick={onClose} type="button"><X size={18} /></button></div><label>Supplier name<input autoFocus maxLength={160} onChange={event => setName(event.target.value)} required value={name} /></label><label>Contact email<input onChange={event => setEmail(event.target.value)} type="email" value={email} /></label>{error ? <p className="inline-error">{error}</p> : null}<button className="command primary" disabled={busy} type="submit">{busy ? <LoaderCircle className="spin" size={17} /> : <Plus size={17} />}Create supplier</button></form></div>;
}

function PurchaseOrderDialog({ products, suppliers, onClose, onSaved }: { products: Product[]; suppliers: Supplier[]; onClose: () => void; onSaved: () => void }) {
  const [supplierId, setSupplierId] = useState(suppliers[0]?.id ?? ""); const [productId, setProductId] = useState(products[0]?.id ?? ""); const [quantity, setQuantity] = useState("1"); const [unitCost, setUnitCost] = useState(products[0] ? String(products[0].price) : ""); const [lines, setLines] = useState<Line[]>([]); const [error, setError] = useState<string | null>(null); const [busy, setBusy] = useState(false);
  function addLine() { const product = products.find(item => item.id === productId); const parsedQuantity = Number(quantity); const parsedCost = Number(unitCost); if (!product || !Number.isInteger(parsedQuantity) || parsedQuantity <= 0 || !Number.isFinite(parsedCost) || parsedCost < 0) { setError("Choose a product with a whole quantity and non-negative unit cost."); return; } if (lines.some(line => line.productId === product.id)) { setError("Each product can appear once in a purchase order."); return; } setLines(current => [...current, { id: crypto.randomUUID(), productId: product.id, productName: product.name, quantity: parsedQuantity, unitCost: parsedCost }]); setError(null); }
  async function submit(event: FormEvent) { event.preventDefault(); if (!supplierId || lines.length === 0) { setError("Choose a supplier and add at least one product line."); return; } setBusy(true); setError(null); try { const response = await fetch("/api/procurement/purchase-orders", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ supplierId, currency: "USD", lines: lines.map(({ productId: lineProductId, productName, quantity: lineQuantity, unitCost: lineUnitCost }) => ({ productId: lineProductId, productName, quantity: lineQuantity, unitCost: lineUnitCost })) }) }); const payload = await response.json().catch(() => null); if (!response.ok) throw new Error(messageOf(payload) ?? "Purchase order could not be created."); onSaved(); } catch (exception) { setError(exception instanceof Error ? exception.message : "Purchase order could not be created."); } finally { setBusy(false); } }
  return <div className="dialog-backdrop"><form className="dialog wide" onSubmit={submit}><div className="dialog-heading"><div><p className="eyebrow">Procurement</p><h2>New purchase order</h2></div><button className="icon-button" aria-label="Close" onClick={onClose} type="button"><X size={18} /></button></div><label>Supplier<select onChange={event => setSupplierId(event.target.value)} required value={supplierId}>{suppliers.map(supplier => <option key={supplier.id} value={supplier.id}>{supplier.name}</option>)}</select></label><div className="po-line-entry"><label>Catalog product<select onChange={event => { const next = products.find(product => product.id === event.target.value); setProductId(event.target.value); if (next) setUnitCost(String(next.price)); }} value={productId}>{products.map(product => <option key={product.id} value={product.id}>{product.name}</option>)}</select></label><label>Quantity<input inputMode="numeric" onChange={event => setQuantity(event.target.value)} value={quantity} /></label><label>Unit cost<input inputMode="decimal" onChange={event => setUnitCost(event.target.value)} value={unitCost} /></label><button className="command" onClick={addLine} type="button"><Plus size={16} />Add line</button></div>{lines.length ? <ul className="po-lines">{lines.map(line => <li key={line.id}><span>{line.quantity} x {line.productName}</span><span>{money.format(line.unitCost)}</span><button aria-label={`Remove ${line.productName}`} className="icon-button" onClick={() => setLines(current => current.filter(item => item.id !== line.id))} type="button"><Trash2 size={15} /></button></li>)}</ul> : <p className="muted-copy">Add catalog products to this purchase order.</p>}{error ? <p className="inline-error">{error}</p> : null}<button className="command primary" disabled={busy || lines.length === 0} type="submit">{busy ? <LoaderCircle className="spin" size={17} /> : <ClipboardPlus size={17} />}Create draft purchase order</button></form></div>;
}

function Metric({ label, value, tone }: { label: string; value: string; tone?: string }) { return <div className={`metric ${tone ?? ""}`}><span>{label}</span><strong>{value}</strong></div>; }
function Status({ status }: { status: PurchaseOrder["status"] }) { const labels: Record<PurchaseOrder["status"], string> = { DRAFT: "Draft", SUBMITTED: "Submitted", RECEIPT_PENDING: "Receipt pending", RECEIVED: "Received", CANCELLED: "Cancelled" }; const tone = status === "RECEIVED" || status === "SUBMITTED" ? "paid" : status === "CANCELLED" ? "failed" : "pending"; return <span className={`status ${tone}`}>{labels[status]}</span>; }
function messageOf(value: unknown): string | null { if (typeof value !== "object" || value === null) return null; const record = value as Record<string, unknown>; return typeof record.message === "string" ? record.message : typeof record.detail === "string" ? record.detail : null; }
