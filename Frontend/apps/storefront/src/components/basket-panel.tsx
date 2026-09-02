"use client";

import { AlertTriangle, LoaderCircle, Minus, Plus, RefreshCw, ShoppingBag, Trash2, X } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { AddressSelection, type AddressLoadState } from "@/components/address-selection";
import type { AddressInput, Basket, CheckoutQuote, CustomerAddress, OrderSummary } from "@/lib/storefront/types";

type BasketLoadState = "idle" | "loading" | "ready" | "unavailable";

type BasketPanelProps = {
  basket: Basket | null;
  loadState: BasketLoadState;
  busyProductId: string | null;
  message: string | null;
  confirmation: OrderSummary | null;
  isCheckingOut: boolean;
  isReviewingCheckout: boolean;
  quote: CheckoutQuote | null;
  addresses: CustomerAddress[];
  addressLoadState: AddressLoadState;
  addressMessage: string | null;
  selectedAddressId: string | null;
  busyAddressId: string | null;
  onCheckout: (couponCode: string, shippingAddressId: string) => void;
  onReview: (couponCode: string, shippingAddressId: string) => void;
  onInvalidateQuote: () => void;
  onSelectAddress: (addressId: string) => void;
  onCreateAddress: (input: AddressInput) => void;
  onUpdateAddress: (addressId: string, input: AddressInput) => void;
  onDeleteAddress: (addressId: string) => void;
  onSetDefaultAddress: (addressId: string) => void;
  onRetryAddresses: () => void;
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
  isReviewingCheckout,
  quote,
  addresses,
  addressLoadState,
  addressMessage,
  selectedAddressId,
  busyAddressId,
  onCheckout,
  onReview,
  onInvalidateQuote,
  onSelectAddress,
  onCreateAddress,
  onUpdateAddress,
  onDeleteAddress,
  onSetDefaultAddress,
  onRetryAddresses,
  onClose,
  onViewOrders,
  onRetry,
  onRefresh,
  onChangeQuantity,
  onRemove,
}: BasketPanelProps) {
  const items = basket?.items ?? [];
  const [couponCode, setCouponCode] = useState("");
  const canCreateOrder = Boolean(
    quote?.canCheckout
    && quote.quoteToken
    && selectedAddressId
    && !isCheckingOut
    && !isReviewingCheckout,
  );
  const checkoutHint = !selectedAddressId
    ? "Select a delivery address before reviewing the order."
    : !quote
      ? "Review the order to confirm current price and availability before creating it."
        : !quote.canCheckout
          ? "Resolve the review issues above before creating the order."
          : null;
  const checkoutHintTone = !selectedAddressId || quote?.canCheckout === false ? "text-[var(--danger)]" : "text-[var(--muted)]";

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
            <div><LoaderCircle aria-hidden="true" className="mx-auto animate-spin" size={26} /><p className="mt-3 text-sm">Loading your cartâ€¦</p></div>
          </div>
        ) : loadState === "unavailable" ? (
          <div className="grid flex-1 place-items-center px-8 text-center">
            <div><ShoppingBag aria-hidden="true" className="mx-auto text-[var(--muted)]" size={30} /><h3 className="mt-4 font-semibold">Your cart could not be loaded</h3><p className="mt-2 text-sm text-[var(--muted)]">Your saved items have not been changed. Try again when you are ready.</p><button className="mt-5 inline-flex h-10 items-center gap-2 border border-[var(--accent)] px-4 text-sm font-semibold text-[var(--accent)] hover:bg-[#e9f2ed]" onClick={onRetry} type="button"><RefreshCw aria-hidden="true" size={16} />Retry</button></div>
          </div>
        ) : items.length === 0 ? (
          <div className="flex flex-1 flex-col overflow-y-auto px-5 py-5">
            <div className="grid flex-1 place-items-center px-3 text-center">
              <div>
              <ShoppingBag aria-hidden="true" className="mx-auto text-[var(--muted)]" size={30} />
              <h3 className="mt-4 font-semibold">Your cart is empty</h3>
              <p className="mt-2 text-sm text-[var(--muted)]">Add products from the catalog when you are ready.</p>
              </div>
            </div>
            <AddressSelection addresses={addresses} busyAddressId={busyAddressId} loadState={addressLoadState} message={addressMessage} onCreate={onCreateAddress} onDelete={onDeleteAddress} onRetry={onRetryAddresses} onSelect={onSelectAddress} onSetDefault={onSetDefaultAddress} onUpdate={onUpdateAddress} selectedAddressId={selectedAddressId} />
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
            <AddressSelection addresses={addresses} busyAddressId={busyAddressId} loadState={addressLoadState} message={addressMessage} onCreate={onCreateAddress} onDelete={onDeleteAddress} onRetry={onRetryAddresses} onSelect={onSelectAddress} onSetDefault={onSetDefaultAddress} onUpdate={onUpdateAddress} selectedAddressId={selectedAddressId} />
          </div>
        )}

        {loadState === "ready" ? <footer className="border-t border-[var(--line)] p-5">
          <div className="flex items-center justify-between text-lg font-semibold">
            <span>Basket subtotal</span><span>{money.format(basket?.totalPrice ?? 0)}</span>
          </div>
          <label className="mt-5 block text-sm font-medium">Promotion code<input className="mt-2 h-10 w-full border border-[var(--line)] bg-white px-3 font-normal outline-none focus:border-[var(--accent)]" disabled={isCheckingOut || isReviewingCheckout} onChange={(event) => { setCouponCode(event.target.value); onInvalidateQuote(); }} placeholder="Optional" value={couponCode} /></label>
          {quote ? <section aria-live="polite" className="mt-4 border border-[var(--line)] bg-[#f7faf8] p-3 text-sm"><div className="flex items-center justify-between gap-3 font-semibold"><span>Reviewed total</span><span>{money.format(quote.totalAmount)}</span></div><p className="mt-1 text-xs leading-5 text-[var(--muted)]">{quote.coupon.message}</p>{quote.items.some((item) => item.priceChanged) ? <p className="mt-2 text-xs font-medium text-[#9a5b11]">Current catalog pricing changed from the basket snapshot.</p> : null}{quote.issues.length > 0 ? <ul className="mt-2 space-y-1 text-xs text-[var(--danger)]">{quote.issues.map((issue) => <li key={`${issue.code}:${issue.productId ?? "order"}`}>{issue.message}</li>)}</ul> : null}{quote.finalRevalidationRequired ? <p className="mt-2 text-xs text-[var(--muted)]">Pricing and availability are checked again when the order is created.</p> : null}</section> : null}
          <p className="mt-3 text-xs leading-5 text-[var(--muted)]">Review uses current catalog, promotion, and inventory data. It does not reserve stock or a promotion.</p>
          {checkoutHint ? <p className={`mt-2 text-xs font-medium ${checkoutHintTone}`} id="create-order-hint" role="status">{checkoutHint}</p> : null}
          <Link className="mt-4 inline-flex h-11 w-full items-center justify-center border border-[var(--line)] px-4 text-sm font-semibold text-[var(--accent)] hover:bg-[#f3f5f2]" href="/checkout">Open full checkout</Link>
          <button className="mt-2 inline-flex h-11 w-full items-center justify-center gap-2 border border-[var(--accent)] px-4 text-sm font-semibold text-[var(--accent)] hover:bg-[#e9f2ed] disabled:cursor-not-allowed disabled:border-[#b5beb6] disabled:text-[#8b948d]" disabled={items.length === 0 || isCheckingOut || isReviewingCheckout || addressLoadState !== "ready" || !selectedAddressId} onClick={() => selectedAddressId && onReview(couponCode, selectedAddressId)} type="button">{isReviewingCheckout ? <span className="animate-pulse">Reviewing order</span> : quote ? "Review order again" : "Review order"}</button>
          <button aria-describedby={checkoutHint ? "create-order-hint" : undefined} className="mt-2 inline-flex h-11 w-full items-center justify-center gap-2 bg-[var(--accent)] px-4 text-sm font-semibold text-white hover:bg-[var(--accent-strong)] disabled:cursor-not-allowed disabled:bg-[#8ba89b]" disabled={items.length === 0 || !canCreateOrder} onClick={() => selectedAddressId && onCheckout(couponCode, selectedAddressId)} title={checkoutHint ?? undefined} type="button">{isCheckingOut ? <span className="animate-pulse">Creating order</span> : "Create order"}</button>
        </footer> : null}
      </aside>
    </div>
  );
}
