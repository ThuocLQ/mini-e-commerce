"use client";

import Link from "next/link";
import { Boxes, CircleAlert, LoaderCircle, LogIn, PackagePlus, Pencil, RefreshCw, ShieldCheck, SlidersHorizontal, X } from "lucide-react";
import { FormEvent, useCallback, useEffect, useMemo, useState } from "react";

type User = { userId: string; userName: string; role: string };
type Product = { id: string; name: string; description: string; price: number; stockQuantity: number };
type Draft = { name: string; description: string; price: string; stockQuantity: string };
const blank: Draft = { name: "", description: "", price: "", stockQuantity: "0" };
const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

export default function OperationsHome() {
  const [session, setSession] = useState<User | null | undefined>(undefined);
  const [products, setProducts] = useState<Product[]>([]);
  const [query, setQuery] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [loginOpen, setLoginOpen] = useState(false);
  const [editor, setEditor] = useState<{ product?: Product } | null>(null);

  const loadProducts = useCallback(async () => {
    setLoading(true); setMessage(null);
    try {
      const response = await fetch("/api/catalog/products");
      const payload = await response.json().catch(() => null);
      if (!response.ok || !Array.isArray(payload)) throw new Error(messageOf(payload) || "Catalog could not be loaded.");
      setProducts(payload as Product[]);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Catalog could not be loaded.");
    } finally { setLoading(false); }
  }, []);

  useEffect(() => {
    async function initialize() {
      try {
        const response = await fetch("/api/session");
        const payload = response.ok ? await response.json() : null;
        const user = isUser(payload && payload.user) ? payload.user : null;
        setSession(user && user.role === "Admin" ? user : null);
        if (user && user.role === "Admin") await loadProducts();
      } catch { setSession(null); }
    }
    void initialize();
  }, [loadProducts]);

  const visible = useMemo(() => {
    const term = query.trim().toLowerCase();
    return term ? products.filter(product => (product.name + " " + product.description + " " + product.id).toLowerCase().includes(term)) : products;
  }, [products, query]);

  if (session === undefined) return <main className="signin"><div><LoaderCircle className="spin" size={22} /></div></main>;
  if (!session) return <SignIn onOpen={() => setLoginOpen(true)} error={message} open={loginOpen} onClose={() => setLoginOpen(false)} onSignedIn={user => { setSession(user); setLoginOpen(false); void loadProducts(); }} />;

  return <main className="shell">
    <aside className="sidebar">
      <div className="brand"><span className="brand-mark"><Boxes size={19} /></span><span>MicroShop</span></div>
      <nav><a className="nav-active" href="#catalog"><Boxes size={17} />Catalog</a><Link href="/inventory">Inventory</Link><Link href="/orders">Orders</Link><Link href="/payments">Payments</Link><Link href="/procurement">Procurement</Link><span className="nav-disabled">Operations</span></nav>
      <div className="operator"><ShieldCheck size={17} /><span>{session.userName}</span><button aria-label="Sign out" title="Sign out" onClick={async () => { await fetch("/api/session", { method: "DELETE" }); setProducts([]); setSession(null); }}><LogIn size={17} /></button></div>
    </aside>
    <section className="workspace" id="catalog">
      <header className="topbar"><div><p className="eyebrow">Operations</p><h1>Catalog control</h1></div><button className="command primary" onClick={() => setEditor({})}><PackagePlus size={17} />New product</button></header>
      <div className="metrics"><Metric label="Products" value={String(products.length)} /><Metric label="Low stock" value={String(products.filter(product => product.stockQuantity > 0 && product.stockQuantity < 10).length)} tone="warn" /><Metric label="Out of stock" value={String(products.filter(product => product.stockQuantity === 0).length)} tone="danger" /></div>
      <div className="toolbar"><label className="search"><SlidersHorizontal size={17} /><input value={query} onChange={event => setQuery(event.target.value)} placeholder="Find product, description or ID" /></label><button className="icon-button" aria-label="Refresh catalog" title="Refresh catalog" disabled={loading} onClick={() => void loadProducts()}>{loading ? <LoaderCircle className="spin" size={18} /> : <RefreshCw size={18} />}</button></div>
      {message ? <div className="notice"><CircleAlert size={19} /><span>{message}</span><button onClick={() => void loadProducts()}>Retry</button></div> : null}
      <div className="table-wrap"><table><thead><tr><th>Product</th><th>Price</th><th>Stock</th><th>Product ID</th><th aria-label="Actions" /></tr></thead><tbody>{visible.map(product => <tr key={product.id}><td><strong>{product.name}</strong><span>{product.description || "No description"}</span></td><td>{money.format(product.price)}</td><td><Stock quantity={product.stockQuantity} /></td><td><code>{product.id}</code></td><td><button className="icon-button" aria-label={"Edit "+product.name} title="Edit product" onClick={() => setEditor({ product })}><Pencil size={16} /></button></td></tr>)}</tbody></table>{!loading && visible.length === 0 ? <div className="empty">No products match this view.</div> : null}</div>
    </section>
    {editor ? <ProductDialog product={editor.product} onClose={() => setEditor(null)} onSaved={() => { setEditor(null); void loadProducts(); }} /> : null}
  </main>;
}

function Metric({ label, value, tone }: { label: string; value: string; tone?: string }) { return <div className={"metric "+(tone || "")}><span>{label}</span><strong>{value}</strong></div>; }
function Stock({ quantity }: { quantity: number }) { return <span className={quantity === 0 ? "stock out" : quantity < 10 ? "stock low" : "stock"}>{quantity === 0 ? "Out of stock" : String(quantity)+" available"}</span>; }
function SignIn({ onOpen, error, open, onClose, onSignedIn }: { onOpen: () => void; error: string | null; open: boolean; onClose: () => void; onSignedIn: (user: User) => void }) {
  const [userName, setUserName] = useState(""); const [password, setPassword] = useState(""); const [busy, setBusy] = useState(false); const [failure, setFailure] = useState<string | null>(null);
  async function submit(event: FormEvent) { event.preventDefault(); setBusy(true); setFailure(null); try { const response = await fetch("/api/session", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ userName, password }) }); const payload = await response.json().catch(() => null); if (!response.ok || !isUser(payload && payload.user)) throw new Error(messageOf(payload) || "Sign-in failed."); onSignedIn(payload.user); } catch (exception) { setFailure(exception instanceof Error ? exception.message : "Sign-in failed."); } finally { setBusy(false); } }
  return <main className="signin"><div><span className="brand-mark"><Boxes size={21} /></span><p className="eyebrow">MicroShop Operations</p><h1>Run the catalog with context.</h1><p>Administrative access is required for this workspace.</p>{error ? <p className="inline-error">{error}</p> : null}<button className="command primary" onClick={onOpen}><LogIn size={17} />Sign in as administrator</button>{open ? <div className="dialog-backdrop"><form className="dialog" onSubmit={submit}><div className="dialog-heading"><div><p className="eyebrow">Administrator access</p><h2>Sign in</h2></div><button className="icon-button" type="button" onClick={onClose} aria-label="Close"><X size={18} /></button></div><label>Username<input value={userName} onChange={event => setUserName(event.target.value)} autoComplete="username" required /></label><label>Password<input value={password} onChange={event => setPassword(event.target.value)} autoComplete="current-password" type="password" required /></label>{failure ? <p className="inline-error">{failure}</p> : null}<button className="command primary" disabled={busy} type="submit">{busy ? <LoaderCircle className="spin" size={17} /> : <LogIn size={17} />}Sign in</button></form></div> : null}</div></main>;
}
function ProductDialog({ product, onClose, onSaved }: { product?: Product; onClose: () => void; onSaved: () => void }) {
  const [draft, setDraft] = useState<Draft>(product ? { name: product.name, description: product.description, price: String(product.price), stockQuantity: String(product.stockQuantity) } : blank); const [error, setError] = useState<string | null>(null); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent) { event.preventDefault(); const price = Number(draft.price); const stockQuantity = Number(draft.stockQuantity); if (!draft.name.trim() || !Number.isFinite(price) || price < 0 || !Number.isInteger(stockQuantity) || stockQuantity < 0) { setError("Enter a name, non-negative price and whole stock quantity."); return; } setBusy(true); setError(null); try { if (!product) { const response = await fetch("/api/catalog/products", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name: draft.name.trim(), description: draft.description.trim(), price, stockQuantity }) }); if (!response.ok) throw new Error(messageOf(await response.json().catch(() => null)) || "Product could not be created."); } else { const id = encodeURIComponent(product.id); const details = await fetch("/api/catalog/products/"+id, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name: draft.name.trim(), description: draft.description.trim(), price }) }); if (!details.ok) throw new Error(messageOf(await details.json().catch(() => null)) || "Product details could not be updated."); const stock = await fetch("/api/catalog/products/"+id+"/stock", { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ stockQuantity }) }); if (!stock.ok) throw new Error("Product details were saved, but stock update failed. Refresh the catalog, then retry the stock change."); } onSaved(); } catch (exception) { setError(exception instanceof Error ? exception.message : "Save failed."); } finally { setBusy(false); } }
  return <div className="dialog-backdrop"><form className="dialog wide" onSubmit={submit}><div className="dialog-heading"><div><p className="eyebrow">{product ? "Edit product" : "New product"}</p><h2>{product ? product.name : "Create catalog item"}</h2></div><button className="icon-button" type="button" onClick={onClose} aria-label="Close"><X size={18} /></button></div><label>Name<input value={draft.name} onChange={event => setDraft({ ...draft, name: event.target.value })} required /></label><label>Description<textarea value={draft.description} onChange={event => setDraft({ ...draft, description: event.target.value })} rows={3} /></label><div className="two-column"><label>Price<input value={draft.price} onChange={event => setDraft({ ...draft, price: event.target.value })} inputMode="decimal" required /></label><label>Available stock<input value={draft.stockQuantity} onChange={event => setDraft({ ...draft, stockQuantity: event.target.value })} inputMode="numeric" required /></label></div>{error ? <p className="inline-error">{error}</p> : null}<button className="command primary" disabled={busy} type="submit">{busy ? <LoaderCircle className="spin" size={17} /> : null}{product ? "Save changes" : "Create product"}</button></form></div>;
}
function isUser(value: unknown): value is User { return typeof value === "object" && value !== null && typeof (value as Record<string, unknown>).userId === "string" && typeof (value as Record<string, unknown>).userName === "string" && typeof (value as Record<string, unknown>).role === "string"; }
function messageOf(value: unknown): string | null { return typeof value === "object" && value !== null && typeof (value as Record<string, unknown>).message === "string" ? (value as { message: string }).message : null; }