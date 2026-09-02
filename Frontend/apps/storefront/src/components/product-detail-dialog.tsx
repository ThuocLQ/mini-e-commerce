"use client";

import { useRef } from "react";
import Link from "next/link";
import { LoaderCircle, ShoppingBag, X } from "lucide-react";
import type { CatalogProduct } from "@/lib/gateway/catalog";
import { ProductImage } from "@/components/product-image";
import { useDialogFocus } from "@/hooks/use-dialog-focus";

type ProductDetailDialogProps = {
  product: CatalogProduct | null;
  busyProductId: string | null;
  onAdd: (product: CatalogProduct) => void;
  onClose: () => void;
};

const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

export function ProductDetailDialog({ product, busyProductId, onAdd, onClose }: ProductDetailDialogProps) {
  const dialogRef = useRef<HTMLElement>(null);
  useDialogFocus({ dialogRef, isOpen: product !== null, onClose });

  if (!product) return null;

  const soldOut = product.stockQuantity <= 0;
  const isBusy = busyProductId === product.id;

  return (
    <div className="fixed inset-0 z-50 grid overflow-y-auto bg-black/35 px-4 py-4 sm:place-items-center sm:py-8" role="presentation">
      <section aria-describedby="product-detail-description" aria-labelledby="product-detail-title" aria-modal="true" className="my-auto w-full max-w-2xl border border-[var(--line)] bg-[var(--surface)] shadow-xl" ref={dialogRef} role="dialog" tabIndex={-1}>
        <header className="flex min-h-16 items-center justify-between border-b border-[var(--line)] px-5">
          <p className="text-sm font-medium text-[var(--accent)]">Product details</p>
          <button aria-label="Close product details" className="grid size-9 place-items-center border border-[var(--line)] text-[var(--muted)] hover:bg-[#f3f5f2]" onClick={onClose} type="button">
            <X aria-hidden="true" size={18} />
          </button>
        </header>

        <div className="grid gap-6 p-5 sm:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)] sm:p-7">
          <div className="min-h-52 overflow-hidden bg-[#edf1ee]"><ProductImage alt={product.name} className="h-full min-h-52 w-full object-contain p-6" fallbackClassName="grid min-h-52 h-full w-full place-items-center text-[var(--muted)]" imageUrl={product.imageUrl} /></div>
          <div>
            {product.category ? <p className="text-sm font-medium text-[var(--accent)]">{product.category}</p> : null}
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
            <Link className="mt-3 inline-flex h-10 w-full items-center justify-center text-sm font-semibold text-[var(--accent)] hover:bg-[#e9f2ed]" href={`/products/${encodeURIComponent(product.id)}`}>Open full product page</Link>
          </div>
        </div>
      </section>
    </div>
  );
}