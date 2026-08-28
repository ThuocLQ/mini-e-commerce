"use client";

import { AlertTriangle, Box, ClipboardList, LoaderCircle, LogIn, RefreshCw, Search, ShoppingBag, UserRound } from "lucide-react";
import Link from "next/link";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { AuthDialog } from "@/components/auth-dialog";
import { EmailVerificationStatus } from "@/components/email-verification-status";
import { type AddressLoadState } from "@/components/address-selection";
import { BasketPanel } from "@/components/basket-panel";
import { OrderPanel } from "@/components/order-panel";
import { ProductDetailDialog } from "@/components/product-detail-dialog";
import { type CatalogProduct, getCatalogProducts } from "@/lib/gateway/catalog";
import { problemMessage } from "@/lib/http/problem-details";
import { productImageSource } from "@/lib/storefront/product-media";
import type { AddressInput, Basket, CheckoutQuote, CurrentUser, CustomerAddress, OrderSummary, PaymentSummary } from "@/lib/storefront/types";

type CatalogState =
  | { status: "loading"; products: CatalogProduct[] }
  | { status: "ready"; products: CatalogProduct[] }
  | { status: "unavailable"; products: CatalogProduct[] };

type SessionState =
  | { status: "loading" }
  | { status: "anonymous" }
  | { status: "authenticated"; user: CurrentUser };

type BasketLoadState = "idle" | "loading" | "ready" | "unavailable";

const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

export function CatalogScreen() {
  const [catalog, setCatalog] = useState<CatalogState>({ status: "loading", products: [] });
  const [session, setSession] = useState<SessionState>({ status: "loading" });
  const [basket, setBasket] = useState<Basket | null>(null);
  const [basketLoadState, setBasketLoadState] = useState<BasketLoadState>("idle");
  const [addresses, setAddresses] = useState<CustomerAddress[]>([]);
  const [addressLoadState, setAddressLoadState] = useState<AddressLoadState>("idle");
  const [addressMessage, setAddressMessage] = useState<string | null>(null);
  const [selectedAddressId, setSelectedAddressId] = useState<string | null>(null);
  const [busyAddressId, setBusyAddressId] = useState<string | null>(null);
  const [query, setQuery] = useState("");
  const [isAuthOpen, setIsAuthOpen] = useState(false);
  const [authNotice, setAuthNotice] = useState<string | null>(null);
  const [isBasketOpen, setIsBasketOpen] = useState(false);
  const [basketMessage, setBasketMessage] = useState<string | null>(null);
  const [busyProductId, setBusyProductId] = useState<string | null>(null);
  const [isCheckingOut, setIsCheckingOut] = useState(false);
  const [isReviewingCheckout, setIsReviewingCheckout] = useState(false);
  const [checkoutQuote, setCheckoutQuote] = useState<CheckoutQuote | null>(null);
  const [orderConfirmation, setOrderConfirmation] = useState<OrderSummary | null>(null);
  const [recentOrder, setRecentOrder] = useState<OrderSummary | null>(null);
  const [orders, setOrders] = useState<OrderSummary[]>([]);
  const [paymentsByOrder, setPaymentsByOrder] = useState<Record<string, PaymentSummary | null>>({});
  const [ordersMessage, setOrdersMessage] = useState<string | null>(null);
  const [isOrdersOpen, setIsOrdersOpen] = useState(false);
  const [isOrdersLoading, setIsOrdersLoading] = useState(false);
  const [startingPaymentOrderId, setStartingPaymentOrderId] = useState<string | null>(null);
  const [completingSandboxPaymentId, setCompletingSandboxPaymentId] = useState<string | null>(null);
  const [cancellingOrderId, setCancellingOrderId] = useState<string | null>(null);
  const [paymentMessage, setPaymentMessage] = useState<string | null>(null);
  const [selectedProduct, setSelectedProduct] = useState<CatalogProduct | null>(null);
  const catalogSectionRef = useRef<HTMLElement>(null);
  const checkoutKeys = useRef(new Map<string, string>());
  const paymentActionKeys = useRef(new Map<string, string>());
  const addressCreateKeys = useRef(new Map<string, string>());

  const recoverExpiredSession = useCallback(() => {
    setSession({ status: "anonymous" });
    setBasket(null);
    setBasketLoadState("idle");
    setAddresses([]);
    setAddressLoadState("idle");
    setAddressMessage(null);
    setSelectedAddressId(null);
    setOrders([]);
    setPaymentsByOrder({});
    setOrderConfirmation(null);
    setRecentOrder(null);
    setIsBasketOpen(false);
    setIsOrdersOpen(false);
    setAuthNotice("Your session has expired. Sign in again to continue.");
    setIsAuthOpen(true);
  }, []);

  function openAuth() {
    setAuthNotice(null);
    setIsAuthOpen(true);
  }

  const loadBasket = useCallback(async (userId: string) => {
    setBasketLoadState("loading");
    try {
      const response = await fetch(`/api/cart/${encodeURIComponent(userId)}`);
      const payload: unknown = await response.json().catch(() => null);
      if (response.status === 401) {
        recoverExpiredSession();
        return;
      }
      if (response.status === 409) {
        throw new Error("Your cart changed in another update. Refresh it before trying again.");
      }
      if (!response.ok || !isBasket(payload)) throw new Error(messageOf(payload) ?? "Your cart could not be loaded.");
      setBasket(payload);
      setBasketLoadState("ready");
    } catch (error) {
      setBasketLoadState("unavailable");
      throw error;
    }
  }, [recoverExpiredSession]);

  const loadAddresses = useCallback(async () => {
    setAddressLoadState("loading");
    try {
      const response = await fetch("/api/addresses");
      const payload: unknown = await response.json().catch(() => null);
      if (response.status === 401) {
        recoverExpiredSession();
        return;
      }
      if (!response.ok || !isAddresses(payload)) throw new Error(messageOf(payload) ?? "Your saved addresses could not be loaded.");
      const activeAddresses = payload.filter((address) => !address.isArchived);
      setAddresses(activeAddresses);
      setSelectedAddressId((current) => current && activeAddresses.some((address) => address.id === current) ? current : activeAddresses.find((address) => address.isDefault)?.id ?? activeAddresses[0]?.id ?? null);
      setAddressLoadState("ready");
    } catch (error) {
      setAddressLoadState("unavailable");
      throw error;
    }
  }, [recoverExpiredSession]);

  const loadOrders = useCallback(async () => {
    setIsOrdersLoading(true);
    setOrdersMessage(null);
    try {
      const response = await fetch("/api/orders");
      const payload: unknown = await response.json().catch(() => null);
      if (response.status === 401) {
        recoverExpiredSession();
        return;
      }
      if (!response.ok || !isOrders(payload)) {
        throw new Error(messageOf(payload) ?? "Your orders could not be loaded.");
      }

      setOrders(payload);
      const paymentEntries = await Promise.all(payload.map(async (order) => {
        try {
          const paymentResponse = await fetch(`/api/payments/orders/${encodeURIComponent(order.id)}`);
          if (paymentResponse.status === 404) return [order.id, null] as const;
          const paymentPayload: unknown = await paymentResponse.json().catch(() => null);
          return [order.id, paymentResponse.ok && isPaymentSummary(paymentPayload) ? paymentPayload : null] as const;
        } catch {
          return [order.id, null] as const;
        }
      }));
      setPaymentsByOrder(Object.fromEntries(paymentEntries));
    } catch (error) {
      setOrdersMessage(error instanceof Error ? error.message : "Your orders could not be loaded.");
    } finally {
      setIsOrdersLoading(false);
    }
  }, [recoverExpiredSession]);
  const requestCatalog = useCallback((keyword?: string, signal?: AbortSignal) => getCatalogProducts({ query: keyword, signal }), []);

  const retryBasket = useCallback(() => {
    if (session.status !== "authenticated") return;
    setBasketMessage(null);
    void loadBasket(session.user.userId).catch((error: unknown) => setBasketMessage(error instanceof Error ? error.message : "Your cart could not be loaded."));
  }, [loadBasket, session]);

  useEffect(() => {
    const controller = new AbortController();
    const keyword = query.trim();
    const timer = window.setTimeout(() => {
      setCatalog((current) => ({ status: "loading", products: current.products }));
      requestCatalog(keyword || undefined, controller.signal)
        .then((products) => setCatalog({ status: "ready", products }))
        .catch((error: unknown) => {
          if (!(error instanceof DOMException && error.name === "AbortError")) {
            setCatalog((current) => ({ status: "unavailable", products: current.products }));
          }
        });
    }, keyword ? 250 : 0);

    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
  }, [query, requestCatalog]);

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
        loadAddresses().catch((error: unknown) => setAddressMessage(error instanceof Error ? error.message : "Your saved addresses could not be loaded."));
      })
      .catch(() => setSession({ status: "anonymous" }));
  }, [loadAddresses, loadBasket]);

  const products = useMemo(() => catalog.products, [catalog.products]);
  const searchTerm = query.trim();
  const catalogSummary = catalog.status === "loading" && searchTerm
    ? `Searching catalog for “${searchTerm}”…`
    : searchTerm
      ? `${products.length} search result${products.length === 1 ? "" : "s"} for “${searchTerm}”`
      : `${products.length} product${products.length === 1 ? "" : "s"} available`;

  const cartCount = basket?.items.reduce((total, item) => total + item.quantity, 0) ?? 0;

  useEffect(() => {
    setCheckoutQuote(null);
  }, [basket?.basketId, basket?.version, selectedAddressId]);

  async function reloadCatalog() {
    setCatalog((current) => ({ ...current, status: "loading" }));
    try {
      setCatalog({ status: "ready", products: await requestCatalog(query.trim() || undefined) });
    } catch {
      setCatalog((current) => ({ status: "unavailable", products: current.products }));
    }
  }

  async function addToBasket(product: CatalogProduct) {
    if (session.status !== "authenticated") {
      openAuth();
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
      if (response.status === 401) {
        recoverExpiredSession();
        return;
      }
      if (!response.ok || !isBasket(payload)) throw new Error(messageOf(payload) ?? "This product could not be added to your cart.");
      setBasket(payload);
      setBasketLoadState("ready");
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
      if (response.status === 401) {
        recoverExpiredSession();
        return;
      }
      if (response.status === 409) {
        setBasketMessage("Your cart changed while this update was being made. Refresh it before continuing.");
        await loadBasket(session.user.userId);
        return;
      }
      if (response.status === 204) return loadBasket(session.user.userId);
      const payload: unknown = await response.json().catch(() => null);
      if (!response.ok || !isBasket(payload)) throw new Error(messageOf(payload) ?? "Your cart could not be updated.");
      setBasket(payload);
      setBasketLoadState("ready");
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
      if (response.status === 401) {
        recoverExpiredSession();
        return;
      }
      if (response.status === 409) {
        setBasketMessage("Your cart changed while this item was being removed. Refresh it before continuing.");
        await loadBasket(session.user.userId);
        return;
      }
      if (response.status === 204) return loadBasket(session.user.userId);
      const payload: unknown = await response.json().catch(() => null);
      throw new Error(messageOf(payload) ?? "This item could not be removed.");
    } catch (error) {
      setBasketMessage(error instanceof Error ? error.message : "This item could not be removed.");
    } finally {
      setBusyProductId(null);
    }
  }

  async function reviewCheckout(couponCode: string, shippingAddressId: string) {
    if (!basket || basketLoadState !== "ready" || !shippingAddressId) return;

    setIsReviewingCheckout(true);
    setBasketMessage(null);
    setOrderConfirmation(null);

    try {
      const response = await fetch("/api/checkout/quote", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          basketId: basket.basketId,
          basketVersion: basket.version,
          shippingAddressId,
          couponCode: couponCode.trim() || undefined,
        }),
      });
      const payload: unknown = await response.json().catch(() => null);
      if (response.status === 401) {
        recoverExpiredSession();
        return;
      }
      if (response.status === 409) {
        setCheckoutQuote(null);
        setBasketMessage("Your cart changed before it could be reviewed. It has been refreshed; review it again.");
        await loadBasket(basket.userId);
        return;
      }
      if (!response.ok || !isCheckoutQuote(payload)) {
        throw new Error(messageOf(payload) ?? "Your order could not be reviewed.");
      }

      setCheckoutQuote(payload);
    } catch (error) {
      setCheckoutQuote(null);
      setBasketMessage(error instanceof Error ? error.message : "Your order could not be reviewed.");
    } finally {
      setIsReviewingCheckout(false);
    }
  }

  async function checkout(couponCode: string, shippingAddressId: string) {
    if (!basket || basketLoadState !== "ready" || !shippingAddressId) return;

    const quote = checkoutQuote;
    if (!quote || !quote.canCheckout || !quote.quoteToken || Date.parse(quote.expiresAtUtc) <= Date.now()) {
      setBasketMessage("Review the current cart before creating an order.");
      return;
    }

    setIsCheckingOut(true);
    setBasketMessage(null);
    setOrderConfirmation(null);

    const scope = basket.basketId + ":" + basket.version + ":" + couponCode.trim().toUpperCase() + ":" + shippingAddressId;
    const idempotencyKey = checkoutKeys.current.get(scope) ?? crypto.randomUUID();
    checkoutKeys.current.set(scope, idempotencyKey);

    try {
      const response = await fetch("/api/checkout", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          basketId: basket.basketId,
          basketVersion: basket.version,
          shippingAddressId,
          couponCode: couponCode.trim() || undefined,
          idempotencyKey,
          quoteToken: quote.quoteToken,
        }),
      });
      const payload: unknown = await response.json().catch(() => null);
      if (response.status === 401) {
        recoverExpiredSession();
        return;
      }
      if (response.status === 409) {
        setCheckoutQuote(null);
        setBasketMessage("The cart, price, promotion, or availability changed before the order could be created. It has been refreshed; review it again.");
        await loadBasket(basket.userId);
        return;
      }
      if (!response.ok || !isOrderSummary(payload)) {
        throw new Error(messageOf(payload) ?? "Your order could not be created.");
      }

      setCheckoutQuote(null);
      setOrderConfirmation(payload);
      setRecentOrder(payload);
      try {
        await loadBasket(basket.userId);
      } catch (refreshError) {
        setBasketMessage(refreshError instanceof Error ? `Your order was created, but ${refreshError.message.toLowerCase()}` : "Your order was created, but your cart could not be refreshed.");
      }
    } catch (error) {
      setBasketMessage(error instanceof Error ? error.message : "Your order could not be created.");
    } finally {
      setIsCheckingOut(false);
    }
  }
  async function createAddress(input: AddressInput) {
    setBusyAddressId("new");
    setAddressMessage(null);
    const scope = JSON.stringify(input);
    const idempotencyKey = addressCreateKeys.current.get(scope) ?? crypto.randomUUID();
    addressCreateKeys.current.set(scope, idempotencyKey);
    try {
      const response = await fetch("/api/addresses", { method: "POST", headers: { "Content-Type": "application/json", "Idempotency-Key": idempotencyKey }, body: JSON.stringify(input) });
      const payload: unknown = await response.json().catch(() => null);
      if (response.status === 401) {
        recoverExpiredSession();
        return;
      }
      if (!response.ok || !isAddress(payload)) throw new Error(messageOf(payload) ?? "This address could not be added.");
      setSelectedAddressId(payload.id);
      await loadAddresses();
    } catch (error) {
      setAddressMessage(error instanceof Error ? error.message : "This address could not be added.");
    } finally {
      setBusyAddressId(null);
    }
  }

  async function updateAddress(addressId: string, input: AddressInput) {
    setBusyAddressId(addressId);
    setAddressMessage(null);
    try {
      const response = await fetch(`/api/addresses/${encodeURIComponent(addressId)}`, { method: "PATCH", headers: { "Content-Type": "application/json" }, body: JSON.stringify(input) });
      const payload: unknown = await response.json().catch(() => null);
      if (response.status === 401) {
        recoverExpiredSession();
        return;
      }
      if (!response.ok || !isAddress(payload)) throw new Error(messageOf(payload) ?? "This address could not be updated.");
      await loadAddresses();
    } catch (error) {
      setAddressMessage(error instanceof Error ? error.message : "This address could not be updated.");
    } finally {
      setBusyAddressId(null);
    }
  }

  async function deleteAddress(addressId: string) {
    setBusyAddressId(addressId);
    setAddressMessage(null);
    try {
      const response = await fetch(`/api/addresses/${encodeURIComponent(addressId)}`, { method: "DELETE" });
      if (response.status === 401) {
        recoverExpiredSession();
        return;
      }
      if (!response.ok) {
        const payload: unknown = await response.json().catch(() => null);
        throw new Error(messageOf(payload) ?? "This address could not be deleted.");
      }
      await loadAddresses();
    } catch (error) {
      setAddressMessage(error instanceof Error ? error.message : "This address could not be deleted.");
    } finally {
      setBusyAddressId(null);
    }
  }

  async function setDefaultAddress(addressId: string) {
    setBusyAddressId(addressId);
    setAddressMessage(null);
    try {
      const response = await fetch(`/api/addresses/${encodeURIComponent(addressId)}/default`, { method: "PUT" });
      if (response.status === 401) {
        recoverExpiredSession();
        return;
      }
      if (!response.ok) {
        const payload: unknown = await response.json().catch(() => null);
        throw new Error(messageOf(payload) ?? "This address could not be made your default.");
      }
      setSelectedAddressId(addressId);
      await loadAddresses();
    } catch (error) {
      setAddressMessage(error instanceof Error ? error.message : "This address could not be made your default.");
    } finally {
      setBusyAddressId(null);
    }
  }

  async function startPayment(orderId: string) {
    setStartingPaymentOrderId(orderId);
    setPaymentMessage(null);
    try {
      const idempotencyKey = paymentActionKeys.current.get(orderId) ?? crypto.randomUUID();
      paymentActionKeys.current.set(orderId, idempotencyKey);
      const response = await fetch("/api/payments", {
        method: "POST",
        headers: { "Content-Type": "application/json", "Idempotency-Key": idempotencyKey },
        body: JSON.stringify({ orderId }),
      });
      const payload: unknown = await response.json().catch(() => null);
      if (response.status === 401) {
        recoverExpiredSession();
        return;
      }
      if (!response.ok || !isPaymentAction(payload)) throw new Error(messageOf(payload) ?? "Payment could not be initiated.");
      setPaymentsByOrder((current) => ({ ...current, [orderId]: payload.payment }));
      setPaymentMessage("Payment action #" + payload.payment.id.slice(0, 8).toUpperCase() + " is " + labelPaymentStatus(payload.payment.status) + " and expires " + new Date(payload.action.expiresAtUtc).toLocaleTimeString() + ". Your order remains awaiting confirmed payment; refresh after the provider callback is processed.");
      await loadOrders();
    } catch (error) {
      setPaymentMessage(error instanceof Error ? error.message : "Payment could not be initiated.");
    } finally {
      setStartingPaymentOrderId(null);
    }
  }

  async function completeSandboxPayment(paymentId: string, orderId: string) {
    setCompletingSandboxPaymentId(paymentId);
    setPaymentMessage(null);
    try {
      const response = await fetch(`/api/payments/${encodeURIComponent(paymentId)}/sandbox-completion`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ outcome: "Approve" }),
      });
      const payload: unknown = await response.json().catch(() => null);
      if (response.status === 401) {
        recoverExpiredSession();
        return;
      }
      if (!response.ok || !isSandboxPaymentCompletion(payload)) {
        throw new Error(messageOf(payload) ?? "Sandbox payment could not be completed.");
      }

      setPaymentsByOrder((current) => ({ ...current, [orderId]: payload.payment }));
      setPaymentMessage("Sandbox provider confirmation was accepted. The order will advance after payment and inventory processing complete.");
      await loadOrders();
    } catch (error) {
      setPaymentMessage(error instanceof Error ? error.message : "Sandbox payment could not be completed.");
    } finally {
      setCompletingSandboxPaymentId(null);
    }
  }

  async function cancelOrder(orderId: string) {
    setCancellingOrderId(orderId);
    setPaymentMessage(null);
    try {
      const response = await fetch(`/api/orders/${encodeURIComponent(orderId)}/cancel`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ reason: "Cancelled by customer from storefront before fulfillment." }),
      });
      const payload: unknown = await response.json().catch(() => null);
      if (response.status === 401) {
        recoverExpiredSession();
        return;
      }
      if (!response.ok || !isOrderSummary(payload)) {
        throw new Error(messageOf(payload) ?? "Your order could not be cancelled.");
      }

      setOrders((current) => current.map((order) => order.id === payload.id ? payload : order));
      setRecentOrder((current) => current?.id === payload.id ? payload : current);
      setPaymentMessage("Order cancellation was recorded. Reservation release and any payment compensation continue safely in the background.");
      await loadOrders();
    } catch (error) {
      setPaymentMessage(error instanceof Error ? error.message : "Your order could not be cancelled.");
    } finally {
      setCancellingOrderId(null);
    }
  }

  function openOrders() {
    setIsOrdersOpen(true);
    void loadOrders();
  }

  function openCatalog() {
    setIsBasketOpen(false);
    setIsOrdersOpen(false);
    catalogSectionRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  function openCart() {
    if (session.status !== "authenticated") {
      openAuth();
      return;
    }
    setIsBasketOpen(true);
    if (basketLoadState === "idle" || basketLoadState === "unavailable") retryBasket();
    if (addressLoadState === "idle" || addressLoadState === "unavailable") {
      setAddressMessage(null);
      void loadAddresses().catch((error: unknown) => setAddressMessage(error instanceof Error ? error.message : "Your saved addresses could not be loaded."));
    }
  }

  function openAccount() {
    if (session.status !== "authenticated") {
      openAuth();
      return;
    }
    openOrders();
  }

  function signedIn(user: CurrentUser) {
    setSession({ status: "authenticated", user });
    setIsAuthOpen(false);
    setAuthNotice(null);
    setBasket(null);
    setBasketLoadState("idle");
    setAddresses([]);
    setAddressLoadState("idle");
    setAddressMessage(null);
    setSelectedAddressId(null);
    loadBasket(user.userId).catch((error: unknown) => setBasketMessage(error instanceof Error ? error.message : "Your cart could not be loaded."));
    loadAddresses().catch((error: unknown) => setAddressMessage(error instanceof Error ? error.message : "Your saved addresses could not be loaded."));
  }

  async function signOut() {
    await fetch("/api/session", { method: "DELETE" });
    setSession({ status: "anonymous" });
    setBasket(null);
    setBasketLoadState("idle");
    setAddresses([]);
    setAddressLoadState("idle");
    setAddressMessage(null);
    setSelectedAddressId(null);
    setOrderConfirmation(null);
    setRecentOrder(null);
    setIsBasketOpen(false);
    setIsOrdersOpen(false);
    setAuthNotice(null);
  }

  return (
    <main className="min-h-screen bg-[var(--background)]">
      <header className="sticky top-0 z-30 border-b border-[var(--line)] bg-[var(--surface)]">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center gap-x-5 gap-y-2 px-4 py-3 sm:px-6 lg:px-8">
          <button aria-label="Browse catalog" className="flex shrink-0 items-center gap-3 text-left" onClick={openCatalog} type="button"><span className="grid size-9 place-items-center bg-[var(--accent)] text-white"><Box aria-hidden="true" size={19} /></span><span><span className="block text-sm font-semibold">MicroShop</span><span className="block text-xs text-[var(--muted)]">Customer store</span></span></button>
          <nav aria-label="Purchase journey" className="order-3 grid w-full grid-cols-3 items-center gap-1 pb-1 sm:order-none sm:flex sm:w-auto sm:flex-1 sm:pb-0">
            <Link className="inline-flex h-9 items-center justify-center px-2 text-sm font-medium text-[var(--accent)] hover:bg-[#e9f2ed] sm:shrink-0 sm:px-3" href="/products">Browse products</Link>
            <button aria-label={`Open cart and checkout, ${cartCount} items`} className="inline-flex h-9 items-center justify-center gap-2 px-2 text-sm font-medium text-[var(--muted)] hover:bg-[#f3f5f2] hover:text-[var(--foreground)] sm:shrink-0 sm:justify-start sm:px-3" onClick={openCart} type="button"><ShoppingBag aria-hidden="true" size={16} /><span className="sm:hidden">Cart</span><span className="hidden sm:inline">Cart &amp; checkout</span>{cartCount ? <span className="grid min-w-5 place-items-center bg-[var(--accent)] px-1 text-xs font-semibold text-white">{cartCount}</span> : null}</button>
            <button className="inline-flex h-9 items-center justify-center gap-2 px-2 text-sm font-medium text-[var(--muted)] hover:bg-[#f3f5f2] hover:text-[var(--foreground)] sm:shrink-0 sm:justify-start sm:px-3" onClick={openAccount} type="button"><ClipboardList aria-hidden="true" size={16} /><span className="sm:hidden">Orders</span><span className="hidden sm:inline">Orders &amp; account</span></button>
          </nav>
          <div className="ml-auto flex items-center gap-2">{session.status === "authenticated" ? <><span className="hidden items-center gap-2 text-sm text-[var(--muted)] lg:inline-flex"><UserRound aria-hidden="true" size={16} />{session.user.userName}<EmailVerificationStatus isVerified={session.user.isEmailVerified} /></span><button className="h-9 px-3 text-sm text-[var(--muted)] hover:text-[var(--foreground)]" onClick={signOut} type="button">Sign out</button></> : <button className="inline-flex h-9 items-center gap-2 px-3 text-sm font-medium text-[var(--accent)] hover:bg-[#e9f2ed]" onClick={openAuth} type="button"><LogIn aria-hidden="true" size={16} />Sign in</button>}</div>
        </div>
      </header>

      <section className="pb-12 pt-7 sm:pt-10" ref={catalogSectionRef}>
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <p className="text-sm font-medium text-[var(--accent)]">Catalog</p>
          <div className="mt-2 flex flex-col gap-5 border-b border-[var(--line)] pb-6 lg:flex-row lg:items-end lg:justify-between">
            <div className="max-w-2xl"><h1 className="text-3xl font-semibold sm:text-4xl">Available products</h1><p className="mt-2 text-sm leading-6 text-[var(--muted)] sm:text-base">Search the current catalog, compare availability, then add only confirmed items to your cart.</p></div>
            <label className="relative block w-full lg:max-w-md"><span className="sr-only">Search products</span><Search aria-hidden="true" className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[var(--muted)]" size={18} /><input className="h-11 w-full border border-[var(--line)] bg-white pl-10 pr-3 text-sm outline-none focus:border-[var(--accent)]" onChange={(event) => setQuery(event.target.value)} placeholder="Search the catalog" type="search" value={query} /></label>
          </div>
          <div aria-live="polite" className="mt-4 flex min-h-5 items-center justify-between gap-4 text-sm text-[var(--muted)]"><p>{catalogSummary}</p>{catalog.status === "loading" && products.length > 0 ? <span className="inline-flex items-center gap-2"><LoaderCircle aria-hidden="true" className="animate-spin" size={15} />Updating catalog…</span> : null}</div>
        </div>

        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">        {catalog.status === "unavailable" ? <div className="mt-8 flex flex-col gap-4 border border-[#f3c5c1] bg-[#fff7f6] p-5 sm:flex-row sm:items-center sm:justify-between"><div className="flex gap-3"><AlertTriangle aria-hidden="true" className="mt-0.5 shrink-0 text-[var(--danger)]" size={20} /><div><h2 className="font-semibold">Catalog is temporarily unavailable</h2><p className="mt-1 text-sm text-[var(--muted)]">Check that the API gateway is running, then try again.</p></div></div><button className="inline-flex h-10 items-center justify-center gap-2 border border-[var(--accent)] px-4 text-sm font-medium text-[var(--accent)] hover:bg-[#e9f2ed]" onClick={reloadCatalog} type="button"><RefreshCw aria-hidden="true" size={16} />Retry</button></div> : null}

        {catalog.status === "loading" && catalog.products.length === 0 ? <CatalogLoading /> : null}
        {catalog.status !== "loading" || catalog.products.length > 0 ? <ProductGrid busyProductId={busyProductId} onAdd={addToBasket} onViewDetails={setSelectedProduct} products={products} query={query} /> : null}
        </div>
      </section>

      <ProductDetailDialog busyProductId={busyProductId} onAdd={addToBasket} onClose={() => setSelectedProduct(null)} product={selectedProduct} />
      <AuthDialog notice={authNotice} onClose={() => { setIsAuthOpen(false); setAuthNotice(null); }} onSignedIn={signedIn} open={isAuthOpen} />
      {isBasketOpen ? <BasketPanel addressLoadState={addressLoadState} addressMessage={addressMessage} addresses={addresses} basket={basket} busyAddressId={busyAddressId} busyProductId={busyProductId} confirmation={orderConfirmation} isCheckingOut={isCheckingOut} isReviewingCheckout={isReviewingCheckout} loadState={basketLoadState} message={basketMessage} onChangeQuantity={changeQuantity} onCheckout={checkout} onClose={() => setIsBasketOpen(false)} onCreateAddress={createAddress} onDeleteAddress={deleteAddress} onInvalidateQuote={() => setCheckoutQuote(null)} onRefresh={retryBasket} onRemove={removeItem} onRetry={retryBasket} onRetryAddresses={() => { setAddressMessage(null); void loadAddresses().catch((error: unknown) => setAddressMessage(error instanceof Error ? error.message : "Your saved addresses could not be loaded.")); }} onReview={reviewCheckout} onSelectAddress={setSelectedAddressId} onSetDefaultAddress={setDefaultAddress} onUpdateAddress={updateAddress} onViewOrders={() => { setIsBasketOpen(false); openOrders(); }} quote={checkoutQuote} selectedAddressId={selectedAddressId} /> : null}
      {isOrdersOpen ? <OrderPanel cancellingOrderId={cancellingOrderId} completingSandboxPaymentId={completingSandboxPaymentId} isLoading={isOrdersLoading} message={ordersMessage} onCancelOrder={cancelOrder} onClose={() => setIsOrdersOpen(false)} onCompleteSandboxPayment={completeSandboxPayment} onRetry={() => void loadOrders()} onStartPayment={startPayment} orders={orders} paymentsByOrder={paymentsByOrder} paymentMessage={paymentMessage} recentOrder={recentOrder} startingPaymentOrderId={startingPaymentOrderId} /> : null}
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
  const source = productImageSource(product.imageUrl);
  if (source) {
    return <div className="h-32 overflow-hidden bg-[#edf1ee]"><img alt={product.name} className="h-full w-full object-contain p-3" loading="lazy" referrerPolicy="no-referrer" src={source} /></div>;
  }

  return <div aria-hidden="true" className="grid h-32 place-items-center bg-[#eef2ef] text-[var(--muted)]"><Box size={34} strokeWidth={1.5} /></div>;
}

function Stock({ quantity }: { quantity: number }) {
  return quantity <= 0 ? <span className="shrink-0 border border-[#f3c5c1] bg-[#fff7f6] px-2 py-1 text-xs font-medium text-[var(--danger)]">Out of stock</span> : <span className="shrink-0 border border-[#b9d7c6] bg-[#f4fbf6] px-2 py-1 text-xs font-medium text-[var(--accent-strong)]">{quantity} in stock</span>;
}

function isBasket(value: unknown): value is Basket {
  if (typeof value !== "object" || value === null) return false;
  const basket = value as Record<string, unknown>;
  return typeof basket.userId === "string" && typeof basket.basketId === "string" && typeof basket.totalPrice === "number" && typeof basket.version === "number" && Array.isArray(basket.items);
}

function isAddress(value: unknown): value is CustomerAddress {
  if (typeof value !== "object" || value === null) return false;
  const address = value as Record<string, unknown>;
  return typeof address.id === "string"
    && typeof address.label === "string"
    && typeof address.recipientName === "string"
    && typeof address.line1 === "string"
    && (typeof address.line2 === "string" || address.line2 === null)
    && typeof address.city === "string"
    && typeof address.countryCode === "string"
    && (typeof address.postalCode === "string" || address.postalCode === null)
    && typeof address.isDefault === "boolean"
    && typeof address.isArchived === "boolean"
    && typeof address.createdAtUtc === "string"
    && typeof address.updatedAtUtc === "string";
}

function isAddresses(value: unknown): value is CustomerAddress[] { return Array.isArray(value) && value.every(isAddress); }

function isSession(value: unknown): value is { user: CurrentUser } {
  if (typeof value !== "object" || value === null) return false;
  const user = (value as { user?: unknown }).user;
  return typeof user === "object" && user !== null && typeof (user as Record<string, unknown>).userId === "string" && typeof (user as Record<string, unknown>).userName === "string" && typeof (user as Record<string, unknown>).role === "string";
}

const messageOf = problemMessage;

function isCheckoutQuote(value: unknown): value is CheckoutQuote {
  if (typeof value !== "object" || value === null) return false;
  const quote = value as Record<string, unknown>;
  return typeof quote.basketId === "string"
    && typeof quote.basketVersion === "number"
    && (typeof quote.quoteToken === "string" || quote.quoteToken === null)
    && typeof quote.canCheckout === "boolean"
    && Array.isArray(quote.issues)
    && typeof quote.evaluatedAtUtc === "string"
    && typeof quote.expiresAtUtc === "string"
    && typeof quote.finalRevalidationRequired === "boolean"
    && typeof quote.currency === "string"
    && typeof quote.subtotalAmount === "number"
    && typeof quote.discountAmount === "number"
    && typeof quote.totalAmount === "number"
    && Array.isArray(quote.items);
}
function isOrderSummary(value: unknown): value is OrderSummary {
  if (typeof value !== "object" || value === null) return false;
  const order = value as Record<string, unknown>;
  return typeof order.id === "string"
    && typeof order.createdAtUtc === "string"
    && typeof order.status === "string"
    && typeof order.totalAmount === "number"
    && typeof order.currency === "string"
    && Array.isArray(order.items)
    && order.items.every(isOrderItem)
    && (order.shippingAddress === null || isShippingAddress(order.shippingAddress));
}

function isShippingAddress(value: unknown) {
  if (typeof value !== "object" || value === null) return false;
  const address = value as Record<string, unknown>;
  return typeof address.addressId === "string"
    && typeof address.label === "string"
    && typeof address.recipientName === "string"
    && typeof address.line1 === "string"
    && (typeof address.line2 === "string" || address.line2 === null)
    && typeof address.city === "string"
    && typeof address.countryCode === "string"
    && (typeof address.postalCode === "string" || address.postalCode === null);
}

function isOrderItem(value: unknown) {
  if (typeof value !== "object" || value === null) return false;
  const item = value as Record<string, unknown>;
  return typeof item.id === "string"
    && typeof item.productId === "string"
    && typeof item.productName === "string"
    && typeof item.unitPrice === "number"
    && typeof item.quantity === "number"
    && typeof item.totalPrice === "number";
}

function isPaymentSummary(value: unknown): value is PaymentSummary {
  if (typeof value !== "object" || value === null) return false;
  const payment = value as Record<string, unknown>;
  return typeof payment.id === "string"
    && typeof payment.orderId === "string"
    && typeof payment.customerId === "string"
    && typeof payment.amount === "number"
    && typeof payment.currency === "string"
    && typeof payment.status === "string"
    && (typeof payment.failureReason === "string" || payment.failureReason === null)
    && typeof payment.createdAtUtc === "string"
    && (typeof payment.completedAtUtc === "string" || payment.completedAtUtc === null)
    && (typeof payment.provider === "string" || payment.provider === null)
    && (typeof payment.paymentActionExpiresAtUtc === "string" || payment.paymentActionExpiresAtUtc === null);
}
function isSandboxPaymentCompletion(value: unknown): value is { payment: PaymentSummary } {
  return typeof value === "object" && value !== null && isPaymentSummary((value as Record<string, unknown>).payment);
}

function isPaymentAction(value: unknown): value is { payment: PaymentSummary; action: { expiresAtUtc: string } } {
  if (typeof value !== "object" || value === null) return false;
  const payload = value as Record<string, unknown>;
  if (typeof payload.payment !== "object" || payload.payment === null || typeof payload.action !== "object" || payload.action === null) return false;
  const payment = payload.payment as Record<string, unknown>;
  const action = payload.action as Record<string, unknown>;
  return isPaymentSummary(payment) && typeof action.expiresAtUtc === "string";
}

function isOrders(value: unknown): value is OrderSummary[] { return Array.isArray(value) && value.every(isOrderSummary); }

function labelPaymentStatus(status: string) { return status.replace(/([a-z])([A-Z])/g, "$1 $2").toLowerCase(); }
