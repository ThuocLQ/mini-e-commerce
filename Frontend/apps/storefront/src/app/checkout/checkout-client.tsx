"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import { ArrowLeft, CheckCircle2, LoaderCircle, ReceiptText, RefreshCw, ShoppingBag } from "lucide-react";
import { AddressSelection, type AddressLoadState } from "@/components/address-selection";
import { problemMessage } from "@/lib/http/problem-details";
import type { Basket, CheckoutQuote, CurrentUser, CustomerAddress, OrderSummary } from "@/lib/storefront/types";

type CheckoutLoadState = "loading" | "ready" | "unauthenticated" | "unavailable";

export function CheckoutClient() {
  const [loadState, setLoadState] = useState<CheckoutLoadState>("loading");
  const [basket, setBasket] = useState<Basket | null>(null);
  const [addresses, setAddresses] = useState<CustomerAddress[]>([]);
  const [selectedAddressId, setSelectedAddressId] = useState<string | null>(null);
  const [couponCode, setCouponCode] = useState("");
  const [quote, setQuote] = useState<CheckoutQuote | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [addressMessage, setAddressMessage] = useState<string | null>(null);
  const [addressLoadState, setAddressLoadState] = useState<AddressLoadState>("loading");
  const [busyAddressId, setBusyAddressId] = useState<string | null>(null);
  const [reviewing, setReviewing] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [createdOrder, setCreatedOrder] = useState<OrderSummary | null>(null);
  const checkoutKeys = useRef(new Map<string, string>());

  const loadCustomerData = useCallback(async (user: CurrentUser) => {
    const [basketResponse, addressesResponse] = await Promise.all([
      fetch(`/api/cart/${encodeURIComponent(user.userId)}`, { cache: "no-store" }),
      fetch("/api/addresses", { cache: "no-store" }),
    ]);
    const basketPayload: unknown = await basketResponse.json().catch(() => null);
    const addressesPayload: unknown = await addressesResponse.json().catch(() => null);
    if (!basketResponse.ok || !isBasket(basketPayload)) throw new Error(problemMessage(basketPayload) ?? "Your cart could not be loaded.");
    if (!addressesResponse.ok || !isAddresses(addressesPayload)) throw new Error(problemMessage(addressesPayload) ?? "Saved addresses could not be loaded.");
    setBasket(basketPayload);
    setAddresses(addressesPayload);
    setSelectedAddressId((current) => current && addressesPayload.some((address) => address.id === current) ? current : addressesPayload.find((address) => address.isDefault)?.id ?? addressesPayload[0]?.id ?? null);
    setAddressLoadState("ready");
  }, []);

  const loadCheckout = useCallback(async () => {
    setLoadState("loading");
    setMessage(null);
    try {
      const sessionResponse = await fetch("/api/session", { cache: "no-store" });
      const sessionPayload: unknown = await sessionResponse.json().catch(() => null);
      if (sessionResponse.status === 401) {
        setLoadState("unauthenticated");
        return;
      }
      if (!sessionResponse.ok || !isSession(sessionPayload)) throw new Error(problemMessage(sessionPayload) ?? "Checkout could not be loaded.");
      await loadCustomerData(sessionPayload.user);
      setLoadState("ready");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Checkout could not be loaded.");
      setLoadState("unavailable");
    }
  }, [loadCustomerData]);

  useEffect(() => { const task = window.setTimeout(() => { void loadCheckout(); }, 0); return () => window.clearTimeout(task); }, [loadCheckout]);

  async function reloadAddresses() {
    setAddressLoadState("loading");
    try {
      const response = await fetch("/api/addresses", { cache: "no-store" });
      const payload: unknown = await response.json().catch(() => null);
      if (!response.ok || !isAddresses(payload)) throw new Error(problemMessage(payload) ?? "Saved addresses could not be loaded.");
      setAddresses(payload);
      setSelectedAddressId((current) => current && payload.some((address) => address.id === current) ? current : payload.find((address) => address.isDefault)?.id ?? payload[0]?.id ?? null);
      setAddressLoadState("ready");
    } catch (error) {
      setAddressMessage(error instanceof Error ? error.message : "Saved addresses could not be loaded.");
      setAddressLoadState("unavailable");
    }
  }

  async function mutateAddress(path: string, method: "POST" | "PATCH" | "PUT" | "DELETE", body?: unknown, busyId = "new") {
    setBusyAddressId(busyId);
    setAddressMessage(null);
    try {
      const response = await fetch(`/api/addresses${path}`, { method, headers: body ? { "Content-Type": "application/json", ...(method === "POST" ? { "Idempotency-Key": crypto.randomUUID() } : {}) } : undefined, body: body ? JSON.stringify(body) : undefined });
      if (!response.ok) {
        const payload: unknown = await response.json().catch(() => null);
        throw new Error(problemMessage(payload) ?? "This address could not be saved.");
      }
      await reloadAddresses();
      setQuote(null);
    } catch (error) {
      setAddressMessage(error instanceof Error ? error.message : "This address could not be saved.");
    } finally { setBusyAddressId(null); }
  }

  async function reviewOrder() {
    if (!basket || !selectedAddressId) { setMessage("Select a delivery address before reviewing the order."); return; }
    setReviewing(true); setMessage(null); setCreatedOrder(null);
    try {
      const response = await fetch("/api/checkout/quote", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ basketId: basket.basketId, basketVersion: basket.version, shippingAddressId: selectedAddressId, couponCode: couponCode.trim() || undefined }) });
      const payload: unknown = await response.json().catch(() => null);
      if (response.status === 409) { await loadCheckout(); throw new Error("Your cart changed while it was being reviewed. Review it again."); }
      if (!response.ok || !isCheckoutQuote(payload)) throw new Error(problemMessage(payload) ?? "Your order could not be reviewed.");
      setQuote(payload);
    } catch (error) { setQuote(null); setMessage(error instanceof Error ? error.message : "Your order could not be reviewed."); }
    finally { setReviewing(false); }
  }

  async function placeOrder() {
    if (!basket || !selectedAddressId || !quote?.canCheckout || !quote.quoteToken || Date.parse(quote.expiresAtUtc) <= Date.now()) { setMessage("Review the current cart again before creating an order."); return; }
    setSubmitting(true); setMessage(null);
    const scope = `${basket.basketId}:${basket.version}:${couponCode.trim().toUpperCase()}:${selectedAddressId}`;
    const idempotencyKey = checkoutKeys.current.get(scope) ?? crypto.randomUUID();
    checkoutKeys.current.set(scope, idempotencyKey);
    try {
      const response = await fetch("/api/checkout", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ basketId: basket.basketId, basketVersion: basket.version, shippingAddressId: selectedAddressId, couponCode: couponCode.trim() || undefined, idempotencyKey, quoteToken: quote.quoteToken }) });
      const payload: unknown = await response.json().catch(() => null);
      if (response.status === 409) { await loadCheckout(); throw new Error("Price, promotion, or availability changed. Review the order again."); }
      if (!response.ok || !isOrder(payload)) throw new Error(problemMessage(payload) ?? "Your order could not be created.");
      setCreatedOrder(payload); setQuote(null); await loadCheckout();
    } catch (error) { setMessage(error instanceof Error ? error.message : "Your order could not be created."); }
    finally { setSubmitting(false); }
  }

  if (loadState === "loading") return <Loading />;
  if (loadState === "unauthenticated") return <SignInRequired />;
  if (loadState === "unavailable" || !basket) return <Unavailable message={message} onRetry={loadCheckout} />;
  if (createdOrder) return <OrderCreated order={createdOrder} />;
  const canPlaceOrder = Boolean(quote?.canCheckout && quote.quoteToken && selectedAddressId && !reviewing && !submitting);
  return <main className="min-h-screen bg-[var(--background)]"><header className="border-b border-[var(--line)] bg-white/95 backdrop-blur"><div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-4 py-3 sm:px-6 lg:px-8"><Link className="inline-flex items-center gap-2 text-sm font-medium text-[var(--accent)] hover:underline" href="/"><ArrowLeft aria-hidden="true" size={16} />Return to cart</Link><span className="text-sm font-semibold tracking-tight">Checkout</span></div></header><div className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8"><header className="border-b border-[var(--line)] pb-6"><p className="text-sm font-medium text-[var(--accent)]">Checkout</p><h1 className="mt-2 text-3xl font-semibold">Review your order</h1><p className="mt-2 max-w-2xl text-sm leading-6 text-[var(--muted)]">Prices, promotions and availability are confirmed before an order is created. Payment happens after the order is confirmed.</p></header>{message ? <p className="mt-6 border-l-2 border-[var(--danger)] bg-[#fff7f6] px-3 py-2 text-sm text-[var(--danger)]" role="alert">{message}</p> : null}<div className="mt-8 grid gap-8 lg:grid-cols-[minmax(0,1fr)_380px]"><section><AddressSelection addresses={addresses} busyAddressId={busyAddressId} loadState={addressLoadState} message={addressMessage} onCreate={(input) => void mutateAddress("", "POST", input)} onDelete={(id) => void mutateAddress(`/${encodeURIComponent(id)}`, "DELETE", undefined, id)} onRetry={() => void reloadAddresses()} onSelect={(id) => { setSelectedAddressId(id); setQuote(null); }} onSetDefault={(id) => void mutateAddress(`/${encodeURIComponent(id)}/default`, "PUT", undefined, id)} onUpdate={(id, input) => void mutateAddress(`/${encodeURIComponent(id)}`, "PATCH", input, id)} selectedAddressId={selectedAddressId} /><section className="mt-8 border-t border-[var(--line)] pt-6"><h2 className="text-xl font-semibold">Promotion</h2><label className="mt-4 block text-sm font-medium">Promotion code<input className="mt-1 h-11 w-full border border-[var(--line)] bg-white px-3 font-normal outline-none focus:border-[var(--accent)]" maxLength={64} onChange={(event) => { setCouponCode(event.target.value); setQuote(null); }} placeholder="Optional" value={couponCode} /></label></section><QuoteReview quote={quote} /></section><aside className="h-fit rounded-sm border border-[var(--line)] bg-[var(--surface)] p-5 lg:sticky lg:top-5"><div className="flex items-center justify-between gap-3"><h2 className="text-lg font-semibold">Order summary</h2><ShoppingBag aria-hidden="true" className="text-[var(--accent)]" size={19} /></div><ul className="mt-5 space-y-3">{basket.items.map((item) => <li className="flex justify-between gap-4 text-sm" key={item.productId}><span className="min-w-0 text-[var(--muted)]">{item.quantity} × {item.productName ?? "Product"}</span><span className="shrink-0 font-medium">{formatMoney(item.price * item.quantity, quote?.currency ?? "USD")}</span></li>)}</ul><div className="mt-5 border-t border-[var(--line)] pt-4"><Row label="Subtotal" value={formatMoney(quote?.subtotalAmount ?? basket.totalPrice, quote?.currency ?? "USD")} />{quote ? <Row label="Promotion" value={quote.discountAmount > 0 ? `-${formatMoney(quote.discountAmount, quote.currency)}` : "None"} /> : null}<Row label="Total" strong value={formatMoney(quote?.totalAmount ?? basket.totalPrice, quote?.currency ?? "USD")} /></div><button className="store-secondary-button mt-6 w-full disabled:opacity-60" disabled={!selectedAddressId || reviewing || submitting || basket.items.length === 0} onClick={() => void reviewOrder()} type="button">{reviewing ? <LoaderCircle aria-hidden="true" className="animate-spin" size={16} /> : <ReceiptText aria-hidden="true" size={16} />}{quote ? "Review order again" : "Review order"}</button><button className="store-primary-button mt-2 w-full disabled:cursor-not-allowed disabled:bg-[#8ba89b]" disabled={!canPlaceOrder} onClick={() => void placeOrder()} type="button">{submitting ? <LoaderCircle aria-hidden="true" className="animate-spin" size={16} /> : null}{submitting ? "Creating order" : "Create order"}</button>{!quote ? <p className="mt-3 text-xs leading-5 text-[var(--muted)]">Review the order to confirm current price and availability before creating it.</p> : null}</aside></div></div></main>;
}

function QuoteReview({ quote }: { quote: CheckoutQuote | null }) { if (!quote) return null; return <section className="mt-8 border-t border-[var(--line)] pt-6"><h2 className="text-xl font-semibold">Review result</h2><p className="mt-2 text-sm text-[var(--muted)]">{quote.canCheckout ? `Reviewed until ${new Date(quote.expiresAtUtc).toLocaleTimeString()}.` : "Resolve the issues below before creating an order."}</p>{quote.issues.length > 0 ? <ul className="mt-4 space-y-2 border-l-2 border-[var(--danger)] bg-[#fff7f6] px-4 py-3 text-sm text-[var(--danger)]">{quote.issues.map((issue, index) => <li key={`${issue.code}-${index}`}>{issue.message}</li>)}</ul> : <p className="mt-4 inline-flex items-center gap-2 text-sm font-medium text-[var(--accent)]"><CheckCircle2 aria-hidden="true" size={17} />Current price and availability are confirmed.</p>}</section>; }
function Row({ label, value, strong }: { label: string; value: string; strong?: boolean }) { return <div className={`mt-2 flex justify-between gap-4 text-sm ${strong ? "pt-2 text-base font-semibold" : "text-[var(--muted)]"}`}><span>{label}</span><span className={strong ? "text-[var(--foreground)]" : "font-medium text-[var(--foreground)]"}>{value}</span></div>; }
function OrderCreated({ order }: { order: OrderSummary }) { return <main className="grid min-h-screen place-items-center bg-[var(--background)] px-4"><section className="max-w-lg border border-[var(--line)] bg-[var(--surface)] p-7 text-center"><CheckCircle2 aria-hidden="true" className="mx-auto text-[var(--accent)]" size={32} /><p className="mt-5 text-sm font-medium text-[var(--accent)]">Order created</p><h1 className="mt-2 text-2xl font-semibold">Order {order.id.slice(0, 8).toUpperCase()} is awaiting payment</h1><p className="mt-3 text-sm leading-6 text-[var(--muted)]">Your order has been confirmed. Open your account to start the payment action and follow its status.</p><Link className="store-primary-button mt-6 px-5" href="/account">Open my account</Link></section></main>; }
function Loading() { return <main className="grid min-h-screen place-items-center bg-[var(--background)] text-[var(--muted)]"><LoaderCircle aria-hidden="true" className="animate-spin" size={25} /></main>; }
function SignInRequired() { return <main className="grid min-h-screen place-items-center bg-[var(--background)] px-4"><section className="max-w-md border border-[var(--line)] bg-[var(--surface)] p-7 text-center"><h1 className="text-xl font-semibold">Sign in before checkout</h1><p className="mt-2 text-sm leading-6 text-[var(--muted)]">Your cart and address book are protected by your account.</p><Link className="mt-6 inline-flex h-10 items-center bg-[var(--accent)] px-4 text-sm font-semibold text-white" href="/">Sign in</Link></section></main>; }
function Unavailable({ message, onRetry }: { message: string | null; onRetry: () => void }) { return <main className="grid min-h-screen place-items-center bg-[var(--background)] px-4"><section className="max-w-md border border-[#f3c5c1] bg-[var(--surface)] p-7 text-center"><h1 className="text-xl font-semibold">Checkout is temporarily unavailable</h1><p className="mt-2 text-sm leading-6 text-[var(--muted)]">{message ?? "Try again shortly."}</p><button className="mt-6 inline-flex h-10 items-center gap-2 border border-[var(--accent)] px-4 text-sm font-semibold text-[var(--accent)]" onClick={onRetry} type="button"><RefreshCw aria-hidden="true" size={16} />Retry</button></section></main>; }
function formatMoney(amount: number, currency: string) { return new Intl.NumberFormat("en-US", { style: "currency", currency }).format(amount); }
function isSession(value: unknown): value is { user: CurrentUser } { return typeof value === "object" && value !== null && typeof (value as { user?: unknown }).user === "object" && (value as { user: Record<string, unknown> }).user !== null && typeof (value as { user: Record<string, unknown> }).user.userId === "string"; }
function isBasket(value: unknown): value is Basket { return typeof value === "object" && value !== null && typeof (value as Record<string, unknown>).userId === "string" && typeof (value as Record<string, unknown>).basketId === "string" && typeof (value as Record<string, unknown>).version === "number" && Array.isArray((value as Record<string, unknown>).items); }
function isAddresses(value: unknown): value is CustomerAddress[] { return Array.isArray(value) && value.every((address) => typeof address === "object" && address !== null && typeof (address as Record<string, unknown>).id === "string"); }
function isCheckoutQuote(value: unknown): value is CheckoutQuote { return typeof value === "object" && value !== null && typeof (value as Record<string, unknown>).canCheckout === "boolean" && ((typeof (value as Record<string, unknown>).quoteToken === "string") || (value as Record<string, unknown>).quoteToken === null) && typeof (value as Record<string, unknown>).expiresAtUtc === "string" && Array.isArray((value as Record<string, unknown>).issues); }
function isOrder(value: unknown): value is OrderSummary { return typeof value === "object" && value !== null && typeof (value as Record<string, unknown>).id === "string" && Array.isArray((value as Record<string, unknown>).items); }