"use client";

import { AlertTriangle, Box, ClipboardList, LoaderCircle, LogIn, RefreshCw, Search, ShoppingBag, UserRound } from "lucide-react";
import Image from "next/image";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { AuthDialog } from "@/components/auth-dialog";
import { BasketPanel } from "@/components/basket-panel";
import { OrderPanel } from "@/components/order-panel";
import { ProductDetailDialog } from "@/components/product-detail-dialog";
import { type CatalogProduct, getCatalogProducts } from "@/lib/gateway/catalog";
import { productImageSource } from "@/lib/storefront/product-media";
import type { Basket, CurrentUser, OrderSummary } from "@/lib/storefront/types";

type CatalogState =
  | { status: "loading"; products: CatalogProduct[] }
  | { status: "ready"; products: CatalogProduct[] }
  | { status: "unavailable"; products: CatalogProduct[] };

type SessionState =
  | { status: "loading" }
  | { status: "anonymous" }
  | { status: "authenticated"; user: CurrentUser };

const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

export function CatalogScreen() {
  const [catalog, setCatalog] = useState<CatalogState>({ status: "loading", products: [] });
  const [session, setSession] = useState<SessionState>({ status: "loading" });
  const [basket, setBasket] = useState<Basket | null>(null);
  const [query, setQuery] = useState("");
  const [isAuthOpen, setIsAuthOpen] = useState(false);
  const [isBasketOpen, setIsBasketOpen] = useState(false);
  const [basketMessage, setBasketMessage] = useState<string | null>(null);
  const [busyProductId, setBusyProductId] = useState<string | null>(null);
  const [isCheckingOut, setIsCheckingOut] = useState(false);
  const [orderConfirmation, setOrderConfirmation] = useState<string | null>(null);
  const [orders, setOrders] = useState<OrderSummary[]>([]);
  const [ordersMessage, setOrdersMessage] = useState<string | null>(null);
  const [isOrdersOpen, setIsOrdersOpen] = useState(false);
  const [isOrdersLoading, setIsOrdersLoading] = useState(false);
  const [isStartingPayment, setIsStartingPayment] = useState(false);
  const [paymentMessage, setPaymentMessage] = useState<string | null>(null);
  const [selectedProduct, setSelectedProduct] = useState<CatalogProduct | null>(null);
  const checkoutKeys = useRef(new Map<string, string>());

  const loadBasket = useCallback(async (userId: string) => {
    const response = await fetch(`/api/cart/${encodeURIComponent(userId)}`);
    const payload: unknown = await response.json().catch(() => null);
    if (!response.ok || !isBasket(payload)) throw new Error(messageOf(payload) ?? "Your cart could not be loaded.");
    setBasket(payload);
  }, []);

  const loadOrders = useCallback(async () => {
    setIsOrdersLoading(true);
    setOrdersMessage(null);
    try {
      const response = await fetch("/api/orders");
      const payload: unknown = await response.json().catch(() => null);
      if (!response.ok || !isOrders(payload)) {
        throw new Error(messageOf(payload) ?? "Your orders could not be loaded.");
      }
      setOrders(payload);
    } catch (error) {
      setOrdersMessage(error instanceof Error ? error.message : "Your orders could not be loaded.");
    } finally {
      setIsOrdersLoading(false);
    }
  }, []);

  const requestCatalog = useCallback((signal?: AbortSignal) => getCatalogProducts(signal), []);

  useEffect(() => {
    const controller = new AbortController();
    requestCatalog(controller.signal)
      .then((products) => setCatalog({ status: "ready", products }))
      .catch((error: unknown) => {
        if (!(error instanceof DOMException && error.name === "AbortError")) {
          setCatalog((current) => ({ status: "unavailable", products: current.products }));
        }
      });
    return () => controller.abort();
  }, [requestCatalog]);

  useEffect(() => {
    fetch("/api/session")
      .then(async (response) => response.ok ? response.json() : null)
      .then((payload: unknown) => {
        if (!isSession(payload)) {
          setSession({ status: "anonymous" });
          return;
        }
        setSession({ status: "authenticated", user: payload.user });
        loadBasket(payload.user.userId).catch((error: unknown) => setBasketMessage(error instanceof Error ? error.message : "Your cart could not be loaded."));
      })
      .catch(() => setSession({ status: "anonymous" }));
  }, [loadBasket]);

  const products = useMemo(() => {
    const terms = query.trim().toLowerCase();
    return terms ? catalog.products.filter((product) => `${product.name} ${product.description}`.toLowerCase().includes(terms)) : catalog.products;
  }, [catalog.products, query]);

  const cartCount = basket?.items.reduce((total, item) => total + item.quantity, 0) ?? 0;

  async function reloadCatalog() {
    setCatalog((current) => ({ ...current, status: "loading" }));
    try {
      setCatalog({ status: "ready", products: await requestCatalog() });
    } catch {
      setCatalog((current) => ({ status: "unavailable", products: current.products }));
    }
  }

  async function addToBasket(product: CatalogProduct) {
    if (session.status !== "authenticated") {
      setIsAuthOpen(true);
      return;
    }

    setBusyProductId(product.id);
    setBasketMessage(null);
    try {
      const response = await fetch(`/api/cart/${encodeURIComponent(session.user.userId)}/items`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ productId: product.id, quantity: 1 }),
      });
      const payload: unknown = await response.json().catch(() => null);
      if (!response.ok || !isBasket(payload)) throw new Error(messageOf(payload) ?? "This product could not be added to your cart.");
      setBasket(payload);
      setSelectedProduct(null);
      setIsBasketOpen(true);
    } catch (error) {
      setBasketMessage(error instanceof Error ? error.message : "This product could not be added to your cart.");
    } finally {
      setBusyProductId(null);
    }
  }

  async function changeQuantity(productId: string, quantity: number) {
    if (session.status !== "authenticated") return;
    setBusyProductId(productId);
    try {
      const response = await fetch(`/api/cart/${encodeURIComponent(session.user.userId)}/items/${encodeURIComponent(productId)}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ quantity }),
      });
      if (response.status === 204) return loadBasket(session.user.userId);
      const payload: unknown = await response.json().catch(() => null);
      if (!response.ok || !isBasket(payload)) throw new Error(messageOf(payload) ?? "Your cart could not be updated.");
      setBasket(payload);
    } catch (error) {
      setBasketMessage(error instanceof Error ? error.message : "Your cart could not be updated.");
    } finally {
      setBusyProductId(null);
    }
  }

  async function removeItem(productId: string) {
    if (session.status !== "authenticated") return;
    setBusyProductId(productId);
    try {
      const response = await fetch(`/api/cart/${encodeURIComponent(session.user.userId)}/items/${encodeURIComponent(productId)}`, { method: "DELETE" });
      if (response.status === 204) return loadBasket(session.user.userId);
      const payload: unknown = await response.json().catch(() => null);
      throw new Error(messageOf(payload) ?? "This item could not be removed.");
    } catch (error) {
      setBasketMessage(error instanceof Error ? error.message : "This item could not be removed.");
    } finally {
      setBusyProductId(null);
    }
  }

  async function checkout(couponCode: string) {
    if (!basket) return;

    setIsCheckingOut(true);
    setBasketMessage(null);
    setOrderConfirmation(null);

    const scope = basket.basketId + ":" + basket.version + ":" + couponCode.trim().toUpperCase();
    const idempotencyKey = checkoutKeys.current.get(scope) ?? crypto.randomUUID();
    checkoutKeys.current.set(scope, idempotencyKey);

    try {
      const response = await fetch("/api/checkout", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          basketId: basket.basketId,
          basketVersion: basket.version,
          couponCode: couponCode.trim() || undefined,
          idempotencyKey,
        }),
      });
      const payload: unknown = await response.json().catch(() => null);
      if (!response.ok || !isOrder(payload)) {
        throw new Error(messageOf(payload) ?? "Your order could not be created.");
      }

      setOrderConfirmation("Order #" + payload.id.slice(0, 8).toUpperCase() + " was created and is awaiting payment.");
      setBasket(null);
    } catch (error) {
      setBasketMessage(error instanceof Error ? error.message : "Your order could not be created.");
    } finally {
      setIsCheckingOut(false);
    }
  }

  async function startPayment(orderId: string) {
    setIsStartingPayment(true);
    setPaymentMessage(null);
    try {
      const response = await fetch("/api/payments", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ orderId }) });
      const payload: unknown = await response.json().catch(() => null);
      if (!response.ok || !isPayment(payload)) throw new Error(messageOf(payload) ?? "Payment could not be initiated.");
      setPaymentMessage("Payment " + payload.id + " was initiated and is awaiting provider authorization.");
      await loadOrders();
    } catch (error) {
      setPaymentMessage(error instanceof Error ? error.message : "Payment could not be initiated.");
    } finally {
      setIsStartingPayment(false);
    }
  }

  function openOrders() {
    setIsOrdersOpen(true);
    void loadOrders();
  }

  function signedIn(user: CurrentUser) {
    setSession({ status: "authenticated", user });
    setIsAuthOpen(false);
    loadBasket(user.userId).catch((error: unknown) => setBasketMessage(error instanceof Error ? error.message : "Your cart could not be loaded."));
  }

  async function signOut() {
    await fetch("/api/session", { method: "DELETE" });
    setSession({ status: "anonymous" });
    setBasket(null);
    setIsBasketOpen(false);
  }

  return (
    <main className="min-h-screen bg-[var(--background)]">
      <header className="border-b border-[var(--line)] bg-[var(--surface)]">
        <div className="mx-auto flex min-h-16 max-w-7xl items-center justify-between gap-3 px-4 sm:px-6 lg:px-8">
          <div className="flex items-center gap-3"><span className="grid size-9 place-items-center bg-[var(--accent)] text-white"><Box aria-hidden="true" size={19} /></span><span className="font-semibold">MicroShop</span></div>
          <nav aria-label="Store navigation" className="flex items-center gap-2">
            {session.status === "authenticated" ? <><span className="hidden items-center gap-2 text-sm text-[var(--muted)] sm:inline-flex"><UserRound aria-hidden="true" size={16} />{session.user.userName}</span><button aria-label="Open your orders" className="grid size-9 place-items-center text-[var(--muted)] hover:bg-[#f3f5f2]" onClick={openOrders} type="button"><ClipboardList aria-hidden="true" size={18} /></button><button className="h-9 px-3 text-sm text-[var(--muted)] hover:text-[var(--foreground)]" onClick={signOut} type="button">Sign out</button></> : <button className="inline-flex h-9 items-center gap-2 px-3 text-sm font-medium text-[var(--accent)] hover:bg-[#e9f2ed]" onClick={() => setIsAuthOpen(true)} type="button"><LogIn aria-hidden="true" size={16} />Sign in</button>}
            <button aria-label={`Open cart, ${cartCount} items`} className="relative grid size-10 place-items-center border border-[var(--line)] hover:bg-[#f3f5f2]" onClick={() => session.status === "authenticated" ? setIsBasketOpen(true) : setIsAuthOpen(true)} type="button"><ShoppingBag aria-hidden="true" size={18} />{cartCount ? <span className="absolute -right-1 -top-1 grid min-w-5 place-items-center bg-[var(--accent)] px-1 text-xs font-semibold text-white">{cartCount}</span> : null}</button>
          </nav>
        </div>
      </header>

      <section className="pb-10">
        <div className="relative min-h-72 overflow-hidden bg-[#17342c] sm:min-h-80">
          <Image alt="Headphones, keyboard, desk organizer and USB-C hub arranged for the MicroShop catalog" className="object-cover object-center" fill priority sizes="100vw" src="/images/catalog-hero.png" />
          <div className="relative mx-auto flex min-h-72 max-w-7xl items-end px-4 py-9 text-white sm:min-h-80 sm:px-6 lg:px-8">
            <div className="max-w-xl">
              <p className="text-sm font-medium text-[#d8e7de]">MicroShop everyday collection</p>
              <h1 className="mt-2 text-3xl font-semibold sm:text-4xl">Tools for a calmer desk.</h1>
              <p className="mt-3 max-w-lg text-sm leading-6 text-[#edf5f0] sm:text-base">Well-chosen essentials for focused work, reliable connection and an easier daily setup.</p>
            </div>
          </div>
        </div>

        <div className="mx-auto flex max-w-7xl flex-col gap-4 px-4 pt-6 sm:flex-row sm:items-center sm:justify-between sm:px-6 lg:px-8">
          <p className="text-sm text-[var(--muted)]">{catalog.products.length} products currently available</p>
          <label className="relative block w-full sm:max-w-sm"><span className="sr-only">Search products</span><Search aria-hidden="true" className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[var(--muted)]" size={18} /><input className="h-11 w-full border border-[var(--line)] bg-white pl-10 pr-3 text-sm outline-none focus:border-[var(--accent)]" onChange={(event) => setQuery(event.target.value)} placeholder="Search products" type="search" value={query} /></label>
        </div>

        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">        {catalog.status === "unavailable" ? <div className="mt-8 flex flex-col gap-4 border border-[#f3c5c1] bg-[#fff7f6] p-5 sm:flex-row sm:items-center sm:justify-between"><div className="flex gap-3"><AlertTriangle aria-hidden="true" className="mt-0.5 shrink-0 text-[var(--danger)]" size={20} /><div><h2 className="font-semibold">Catalog is temporarily unavailable</h2><p className="mt-1 text-sm text-[var(--muted)]">Check that the API gateway is running, then try again.</p></div></div><button className="inline-flex h-10 items-center justify-center gap-2 border border-[var(--accent)] px-4 text-sm font-medium text-[var(--accent)] hover:bg-[#e9f2ed]" onClick={reloadCatalog} type="button"><RefreshCw aria-hidden="true" size={16} />Retry</button></div> : null}

        {catalog.status === "loading" && catalog.products.length === 0 ? <CatalogLoading /> : null}
        {catalog.status !== "loading" || catalog.products.length > 0 ? <ProductGrid busyProductId={busyProductId} onAdd={addToBasket} onViewDetails={setSelectedProduct} products={products} query={query} /> : null}
        </div>
      </section>

      <ProductDetailDialog busyProductId={busyProductId} onAdd={addToBasket} onClose={() => setSelectedProduct(null)} product={selectedProduct} />
      <AuthDialog onClose={() => setIsAuthOpen(false)} onSignedIn={signedIn} open={isAuthOpen} />
      {isBasketOpen ? <BasketPanel basket={basket} busyProductId={busyProductId} confirmation={orderConfirmation} isCheckingOut={isCheckingOut} message={basketMessage} onChangeQuantity={changeQuantity} onCheckout={checkout} onClose={() => setIsBasketOpen(false)} onRemove={removeItem} onViewOrders={() => { setIsBasketOpen(false); openOrders(); }} /> : null}
      {isOrdersOpen ? <OrderPanel isLoading={isOrdersLoading} isStartingPayment={isStartingPayment} message={ordersMessage} onClose={() => setIsOrdersOpen(false)} onStartPayment={startPayment} orders={orders} paymentMessage={paymentMessage} /> : null}
    </main>
  );
}

function CatalogLoading() {
  return <div aria-label="Loading products" className="mt-8 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">{Array.from({ length: 8 }, (_, index) => <div className="h-72 border border-[var(--line)] bg-white p-4" key={index}><div className="h-28 animate-pulse bg-[#e8ece8]" /><div className="mt-5 h-4 w-3/4 animate-pulse bg-[#e8ece8]" /><div className="mt-3 h-3 animate-pulse bg-[#e8ece8]" /></div>)}</div>;
}

function ProductGrid({ busyProductId, onAdd, onViewDetails, products, query }: { busyProductId: string | null; onAdd: (product: CatalogProduct) => void; onViewDetails: (product: CatalogProduct) => void; products: CatalogProduct[]; query: string }) {
  if (products.length === 0) return <div className="mt-8 border border-dashed border-[var(--line)] bg-white px-6 py-14 text-center"><Box aria-hidden="true" className="mx-auto text-[var(--muted)]" size={28} /><h2 className="mt-4 text-lg font-semibold">{query ? "No matching products" : "No products available"}</h2><p className="mt-2 text-sm text-[var(--muted)]">{query ? "Try another product name or description." : "Products will appear here when they are published."}</p></div>;

  return <div className="mt-8 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">{products.map((product) => <article className="flex min-h-72 flex-col border border-[var(--line)] bg-white p-4" key={product.id}><ProductMedia product={product} /><div className="mt-5 flex items-start justify-between gap-3"><h2 className="text-base font-semibold leading-6">{product.name}</h2><Stock quantity={product.stockQuantity} /></div><p className="mt-2 line-clamp-2 text-sm leading-5 text-[var(--muted)]">{product.description || "No product description is available."}</p><div className="mt-auto flex items-center justify-between gap-3 pt-6"><p className="text-lg font-semibold">{money.format(product.price)}</p><div className="flex items-center gap-2"><button className="h-10 px-3 text-sm font-semibold text-[var(--accent)] hover:bg-[#e9f2ed]" onClick={() => onViewDetails(product)} type="button">Details</button><button className="inline-flex h-10 items-center gap-2 bg-[var(--accent)] px-3 text-sm font-semibold text-white hover:bg-[var(--accent-strong)] disabled:cursor-not-allowed disabled:bg-[#8ba89b]" disabled={product.stockQuantity <= 0 || busyProductId === product.id} onClick={() => onAdd(product)} type="button">{busyProductId === product.id ? <LoaderCircle aria-hidden="true" className="animate-spin" size={16} /> : <ShoppingBag aria-hidden="true" size={16} />}{busyProductId === product.id ? "Adding" : "Add"}</button></div></div></article>)}</div>;
}

function ProductMedia({ product }: { product: CatalogProduct }) {
  const source = productImageSource(product.name);
  if (source) {
    return <div className="relative h-32 overflow-hidden bg-[#edf1ee]"><Image alt={product.name} className="object-contain p-3" fill sizes="(min-width: 1280px) 280px, (min-width: 1024px) 30vw, (min-width: 640px) 45vw, 90vw" src={source} /></div>;
  }

  const tones = ["#e3eee7", "#e8edf5", "#f3ebdd", "#f0e7ec"];
  const tone = tones[Math.abs(hash(product.id)) % tones.length];
  return <div aria-hidden="true" className="grid h-32 place-items-center" style={{ backgroundColor: tone }}><Box size={34} strokeWidth={1.5} /></div>;
}

function Stock({ quantity }: { quantity: number }) {
  return quantity <= 0 ? <span className="shrink-0 border border-[#f3c5c1] bg-[#fff7f6] px-2 py-1 text-xs font-medium text-[var(--danger)]">Out of stock</span> : <span className="shrink-0 border border-[#b9d7c6] bg-[#f4fbf6] px-2 py-1 text-xs font-medium text-[var(--accent-strong)]">{quantity} in stock</span>;
}

function isBasket(value: unknown): value is Basket {
  if (typeof value !== "object" || value === null) return false;
  const basket = value as Record<string, unknown>;
  return typeof basket.userId === "string" && typeof basket.basketId === "string" && typeof basket.totalPrice === "number" && typeof basket.version === "number" && Array.isArray(basket.items);
}

function isSession(value: unknown): value is { user: CurrentUser } {
  if (typeof value !== "object" || value === null) return false;
  const user = (value as { user?: unknown }).user;
  return typeof user === "object" && user !== null && typeof (user as Record<string, unknown>).userId === "string" && typeof (user as Record<string, unknown>).userName === "string" && typeof (user as Record<string, unknown>).role === "string";
}

function messageOf(value: unknown): string | null {
  return typeof value === "object" && value !== null && typeof (value as Record<string, unknown>).message === "string" ? (value as Record<string, string>).message : null;
}

function hash(value: string) {
  return Array.from(value).reduce((total, character) => ((total << 5) - total + character.charCodeAt(0)) | 0, 0);
}

function isOrder(value: unknown): value is { id: string; status: string } {
  return typeof value === "object" && value !== null
    && typeof (value as Record<string, unknown>).id === "string"
    && typeof (value as Record<string, unknown>).status === "string";
}
function isPayment(value: unknown): value is { id: string } { return typeof value === "object" && value !== null && typeof (value as Record<string, unknown>).id === "string"; }

function isOrders(value: unknown): value is OrderSummary[] {
  return Array.isArray(value) && value.every((order) => typeof order === "object" && order !== null
    && typeof (order as Record<string, unknown>).id === "string"
    && typeof (order as Record<string, unknown>).createdAtUtc === "string"
    && typeof (order as Record<string, unknown>).status === "string"
    && typeof (order as Record<string, unknown>).totalAmount === "number"
    && typeof (order as Record<string, unknown>).currency === "string"
    && Array.isArray((order as Record<string, unknown>).items));
}