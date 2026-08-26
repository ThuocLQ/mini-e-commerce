"use client";

import Link from "next/link";
import { ArrowLeft, Boxes, CircleAlert, LoaderCircle, RefreshCw } from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";

type InventoryItem = { productId: string; stockQuantity: number; reservedQuantity: number; availableQuantity: number; updatedAtUtc: string };
type Product = { id: string; name: string; description: string };
type Row = InventoryItem & { name: string; description: string };
const dateTime = new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeStyle: "short" });

export default function InventoryPage() {
  const [items, setItems] = useState<Row[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const getInventory = useCallback(async () => {
    const [inventoryResponse, catalogResponse] = await Promise.all([
      fetch("/api/inventory/admin/items"),
      fetch("/api/catalog/products"),
    ]);
    const inventory = await inventoryResponse.json().catch(() => null);
    const catalog = await catalogResponse.json().catch(() => null);
    if (!inventoryResponse.ok || !Array.isArray(inventory)) throw new Error(messageOf(inventory) ?? "Inventory could not be loaded.");

    const products = Array.isArray(catalog) ? catalog as Product[] : [];
    const productById = new Map(products.map(product => [product.id, product]));
    return (inventory as InventoryItem[]).map(item => {
      const product = productById.get(item.productId);
      return { ...item, name: product?.name ?? "Unknown catalog item", description: product?.description ?? item.productId };
    });
  }, []);

  const load = useCallback(async () => {
    setLoading(true); setError(null);
    try { setItems(await getInventory()); }
    catch (exception) { setError(exception instanceof Error ? exception.message : "Inventory could not be loaded."); }
    finally { setLoading(false); }
  }, [getInventory]);

  useEffect(() => {
    let active = true;
    async function loadInitialInventory() {
      try { const result = await getInventory(); if (active) setItems(result); }
      catch (exception) { if (active) setError(exception instanceof Error ? exception.message : "Inventory could not be loaded."); }
      finally { if (active) setLoading(false); }
    }
    void loadInitialInventory();
    return () => { active = false; };
  }, [getInventory]);

  const summary = useMemo(() => ({
    available: items.reduce((sum, item) => sum + item.availableQuantity, 0),
    reserved: items.reduce((sum, item) => sum + item.reservedQuantity, 0),
    low: items.filter(item => item.availableQuantity > 0 && item.availableQuantity < 10).length,
  }), [items]);

  return <main className="orders-page"><header className="orders-header"><Link className="back" href="/"><ArrowLeft size={17} />Catalog control</Link><div className="orders-title"><div><p className="eyebrow">Operations</p><h1>Inventory availability</h1></div><button className="icon-button" aria-label="Refresh inventory" title="Refresh inventory" disabled={loading} onClick={() => void load()}>{loading ? <LoaderCircle className="spin" size={18} /> : <RefreshCw size={18} />}</button></div></header><div className="metrics"><Metric label="Available units" value={String(summary.available)} /><Metric label="Reserved units" value={String(summary.reserved)} tone="warn" /><Metric label="Low availability" value={String(summary.low)} tone="danger" /></div>{error ? <div className="notice"><CircleAlert size={19} /><span>{error}</span><button onClick={() => void load()}>Retry</button></div> : null}<div className="table-wrap"><table><thead><tr><th>Product</th><th>Total stock</th><th>Reserved</th><th>Available</th><th>Last update</th></tr></thead><tbody>{items.map(item => <tr key={item.productId}><td><strong><Boxes size={16} />{item.name}</strong><span>{item.description}</span></td><td>{item.stockQuantity}</td><td>{item.reservedQuantity}</td><td><Availability quantity={item.availableQuantity} /></td><td>{dateTime.format(new Date(item.updatedAtUtc))}</td></tr>)}</tbody></table>{!loading && items.length === 0 ? <div className="empty">No inventory records are available yet.</div> : null}</div></main>;
}

function Metric({ label, value, tone }: { label: string; value: string; tone?: string }) { return <div className={"metric " + (tone ?? "")}><span>{label}</span><strong>{value}</strong></div>; }
function Availability({ quantity }: { quantity: number }) { const className = quantity === 0 ? "stock out" : quantity < 10 ? "stock low" : "stock"; return <span className={className}>{quantity === 0 ? "Out of stock" : String(quantity) + " available"}</span>; }
function messageOf(value: unknown): string | null { return typeof value === "object" && value !== null && typeof (value as Record<string, unknown>).message === "string" ? (value as { message: string }).message : null; }
