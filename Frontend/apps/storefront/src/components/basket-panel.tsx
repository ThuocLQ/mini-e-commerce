"use client";

import { AlertTriangle, LoaderCircle, Minus, Plus, RefreshCw, ShoppingBag, Trash2, X } from "lucide-react";
import { useState } from "react";
import type { Basket, OrderSummary } from "@/lib/storefront/types";

type BasketLoadState = "idle" | "loading" | "ready" | "unavailable";

type BasketPanelProps = {
  basket: Basket | null;
  loadState: BasketLoadState;
  busyProductId: string | null;
  message: string | null;
  confirmation: OrderSummary | null;
  isCheckingOut: boolean;
  onCheckout: (couponCode: string) => void;
  onClose: () => void;
  onViewOrders: () => void;
  onRetry: () => void;
  onRefresh: () => void;
  onChangeQuantity: (productId: string, quantity: number) => void;
  onRemove: (productId: string) => void;
};

const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

export function BasketPanel({
  basket,
  loadState,
  busyProductId,
  message,
  confirmation,
  isCheckingOut,
  onCheckout,
  onClose,
  onViewOrders,
  onRetry,
  onRefresh,
  onChangeQuantity,
  onRemove,
}: BasketPanelProps) {
  const items = basket?.items ?? [];
  const [couponCode, setCouponCode] = useState("");

  return (
    <div className="fixed inset-0 z-40 flex justify-end bg-black/35" role="presentation">
      <aside aria-label="Cart and checkout" className="flex h-full w-full max-w-md flex-col bg-[var(--surface)] shadow-xl">
        <header className="flex min-h-16 items-center justify-between border-b border-[var(--line)] px-5">
          <div className="flex items-center gap-3">
            <ShoppingBag aria-hidden="true" size={19} />
            <h2 className="font-semibold">Cart &amp; checkout</h2>
          </div>
          <button aria-label="Close cart" className="grid size-9 place-items-center border border-[var(--line)] text-[var(--muted)] hover:bg-[#f3f5f2]" onClick={onClose} type="button">
            <X aria-hidden="true" size={18} />
          </button>
        </header>
        {message ? <div className="mx-5 mt-4 border-l-2 border-[var(--danger)] bg-[#fff7f6] px-3 py-3 text-sm text-[var(--danger)]" role="alert"><div className="flex gap-2"><AlertTriangle aria-hidden="true" className="mt-0.5 shrink-0" size={16} /><p>{message}</p></div><button className="mt-3 inline-flex h-9 items-center gap-2 border border-[var(--danger)] px-3 text-sm font-semibold hover:bg-white" onClick={onRefresh} type="button"><RefreshCw aria-hidden="true" size={15} />Refresh cart</button></div> : null}
        {confirmation ? <div className="mx-5 mt-4 border-l-2 border-[var(--accent)] bg-[#f4fbf6] px-3 py-3 text-sm text-[var(--accent-strong)]" role="status"><p>Order #{confirmation.id.slice(0, 8).toUpperCase()} was created and is awaiting payment.</p><button className="mt-3 inline-flex h-9 items-center border border-[var(--accent)] px-3 text-sm font-semibold text-[var(--accent)] hover:bg-white" onClick={onViewOrders} type="button">View order status</button></div> : null}

        {loadState === "loading" ? (
          <div aria-live="polite" className="grid flex-1 place-items-center px-8 text-center text-[var(--muted)]">
            <div><LoaderCircle aria-hidden="true" className="mx-auto animate-spin" size={26} /><p className="mt-3 text-sm">Loading your cart…</p></div>
          </div>
        ) : loadState === "unavailable" ? (
          <div className="grid flex-1 place-items-center px-8 text-center">
            <div><ShoppingBag aria-hidden="true" className="mx-auto text-[var(--muted)]" size={30} /><h3 className="mt-4 font-semibold">Your cart could not be loaded</h3><p className="mt-2 text-sm text-[var(--muted)]">Your saved items have not been changed. Try again when you are ready.</p><button className="mt-5 inline-flex h-10 items-center gap-2 border border-[var(--accent)] px-4 text-sm font-semibold text-[var(--accent)] hover:bg-[#e9f2ed]" onClick={onRetry} type="button"><RefreshCw aria-hidden="true" size={16} />Retry</button></div>
          </div>
        ) : items.length === 0 ? (
          <div className="grid flex-1 place-items-center px-8 text-center">
            <div>
              <ShoppingBag aria-hidden="true" className="mx-auto text-[var(--muted)]" size={30} />
              <h3 className="mt-4 font-semibold">Your cart is empty</h3>
              <p className="mt-2 text-sm text-[var(--muted)]">Add products from the catalog when you are ready.</p>
            </div>
          </div>
        ) : (
          <div className="flex-1 overflow-y-auto px-5 py-5">
            <ul className="space-y-4">
              {items.map((item) => {
                const isBusy = busyProductId === item.productId;
                return (
                  <li className="border-b border-[var(--line)] pb-4" key={item.productId}>
                    <div className="flex justify-between gap-4">
                      <div>
                        <h3 className="font-medium">{item.productName ?? "Product"}</h3>
                        <p className="mt-1 text-sm text-[var(--muted)]">{money.format(item.price)} each</p>
                      </div>
                      <p className="font-semibold">{money.format(item.price * item.quantity)}</p>
                    </div>
                    <div className="mt-4 flex items-center justify-between">
                      <div className="inline-flex h-9 items-center border border-[var(--line)]">
                        <button aria-label={`Decrease ${item.productName ?? "item"} quantity`} className="grid h-full w-9 place-items-center hover:bg-[#f3f5f2] disabled:text-[#b5beb6]" disabled={isBusy} onClick={() => onChangeQuantity(item.productId, item.quantity - 1)} type="button"><Minus aria-hidden="true" size={15} /></button>
                        <span aria-live="polite" className="grid h-full min-w-9 place-items-center border-x border-[var(--line)] px-2 text-sm">{item.quantity}</span>
                        <button aria-label={`Increase ${item.productName ?? "item"} quantity`} className="grid h-full w-9 place-items-center hover:bg-[#f3f5f2] disabled:text-[#b5beb6]" disabled={isBusy} onClick={() => onChangeQuantity(item.productId, item.quantity + 1)} type="button"><Plus aria-hidden="true" size={15} /></button>
                      </div>
                      <button aria-label={`Remove ${item.productName ?? "item"}`} className="grid size-9 place-items-center text-[var(--danger)] hover:bg-[#fff7f6] disabled:text-[#d9a6a1]" disabled={isBusy} onClick={() => onRemove(item.productId)} type="button"><Trash2 aria-hidden="true" size={17} /></button>
                    </div>
                  </li>
                );
              })}
            </ul>
          </div>
        )}

        {loadState === "ready" ? <footer className="border-t border-[var(--line)] p-5">
          <div className="flex items-center justify-between text-lg font-semibold">
            <span>Subtotal</span><span>{money.format(basket?.totalPrice ?? 0)}</span>
          </div>
          <label className="mt-5 block text-sm font-medium">Promotion code<input className="mt-2 h-10 w-full border border-[var(--line)] bg-white px-3 font-normal outline-none focus:border-[var(--accent)]" disabled={isCheckingOut} onChange={(event) => setCouponCode(event.target.value)} placeholder="Optional" value={couponCode} /></label>
          <p className="mt-3 text-xs leading-5 text-[var(--muted)]">We will confirm current prices and availability before creating an order. Payment is not complete at this step.</p><button className="mt-4 inline-flex h-11 w-full items-center justify-center gap-2 bg-[var(--accent)] px-4 text-sm font-semibold text-white hover:bg-[var(--accent-strong)] disabled:cursor-not-allowed disabled:bg-[#8ba89b]" disabled={items.length === 0 || isCheckingOut} onClick={() => onCheckout(couponCode)} type="button">{isCheckingOut ? <span className="animate-pulse">Creating order</span> : "Create order"}</button>
        </footer> : null}
      </aside>
    </div>
  );
}
