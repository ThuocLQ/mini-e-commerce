"use client";

import Image from "next/image";
import { useEffect } from "react";
import { Box, LoaderCircle, ShoppingBag, X } from "lucide-react";
import type { CatalogProduct } from "@/lib/gateway/catalog";
import { productImageSource } from "@/lib/storefront/product-media";

type ProductDetailDialogProps = {
  product: CatalogProduct | null;
  busyProductId: string | null;
  onAdd: (product: CatalogProduct) => void;
  onClose: () => void;
};

const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

export function ProductDetailDialog({ product, busyProductId, onAdd, onClose }: ProductDetailDialogProps) {
  useEffect(() => {
    if (!product) return;

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") onClose();
    }

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [onClose, product]);
  if (!product) return null;

  const soldOut = product.stockQuantity <= 0;
  const isBusy = busyProductId === product.id;
  const source = productImageSource(product.name);

  return (
    <div className="fixed inset-0 z-50 grid overflow-y-auto bg-black/35 px-4 py-4 sm:place-items-center sm:py-8" role="presentation">
      <section aria-describedby="product-detail-description" aria-labelledby="product-detail-title" aria-modal="true" className="my-auto w-full max-w-2xl border border-[var(--line)] bg-[var(--surface)] shadow-xl" role="dialog">
        <header className="flex min-h-16 items-center justify-between border-b border-[var(--line)] px-5">
          <p className="text-sm font-medium text-[var(--accent)]">Product details</p>
          <button aria-label="Close product details" className="grid size-9 place-items-center border border-[var(--line)] text-[var(--muted)] hover:bg-[#f3f5f2]" onClick={onClose} type="button">
            <X aria-hidden="true" size={18} />
          </button>
        </header>

        <div className="grid gap-6 p-5 sm:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)] sm:p-7">
          {source ? <div className="relative min-h-52 overflow-hidden bg-[#edf1ee]"><Image alt={product.name} className="object-contain p-6" fill sizes="(min-width: 640px) 380px, 90vw" src={source} /></div> : <div aria-hidden="true" className="grid min-h-52 place-items-center bg-[#e8edf5] text-[#425a77]"><Box size={56} strokeWidth={1.25} /></div>}
          <div>
            <h2 className="text-2xl font-semibold" id="product-detail-title">{product.name}</h2>
            <p className="mt-3 text-sm leading-6 text-[var(--muted)]" id="product-detail-description">{product.description || "No product description is available."}</p>
            <dl className="mt-6 divide-y divide-[var(--line)] border-y border-[var(--line)] text-sm">
              <div className="flex items-center justify-between gap-4 py-3"><dt className="text-[var(--muted)]">Price</dt><dd className="font-semibold">{money.format(product.price)}</dd></div>
              <div className="flex items-center justify-between gap-4 py-3"><dt className="text-[var(--muted)]">Availability</dt><dd className={soldOut ? "font-medium text-[var(--danger)]" : "font-medium text-[var(--accent-strong)]"}>{soldOut ? "Out of stock" : product.stockQuantity + " available"}</dd></div>
            </dl>
            <button className="mt-6 inline-flex h-11 w-full items-center justify-center gap-2 bg-[var(--accent)] px-4 text-sm font-semibold text-white hover:bg-[var(--accent-strong)] disabled:cursor-not-allowed disabled:bg-[#8ba89b]" disabled={soldOut || isBusy} onClick={() => onAdd(product)} type="button">
              {isBusy ? <LoaderCircle aria-hidden="true" className="animate-spin" size={17} /> : <ShoppingBag aria-hidden="true" size={17} />}
              {soldOut ? "Unavailable" : isBusy ? "Adding to cart" : "Add to cart"}
            </button>
          </div>
        </div>
      </section>
    </div>
  );
}