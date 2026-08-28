import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, Box, ShoppingBag } from "lucide-react";
import { ProductPurchaseActions } from "@/components/product-purchase-actions";
import { getCatalogProduct } from "@/lib/gateway/catalog-server";
import type { CatalogProduct } from "@/lib/gateway/catalog";
import { productImageSource } from "@/lib/storefront/product-media";

const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

export default async function ProductDetailPage({ params }: { params: Promise<{ productId: string }> }) {
  const { productId } = await params;
  const result = await getCatalogProduct(productId);

  return <main className="min-h-screen bg-[var(--background)]">
    <header className="border-b border-[var(--line)] bg-[var(--surface)]"><div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-4 py-3 sm:px-6 lg:px-8"><Link className="flex items-center gap-3 text-left" href="/"><span className="grid size-9 place-items-center bg-[var(--accent)] text-white"><Box aria-hidden="true" size={19} /></span><span><span className="block text-sm font-semibold">MicroShop</span><span className="block text-xs text-[var(--muted)]">Customer store</span></span></Link><Link className="inline-flex items-center gap-2 text-sm font-medium text-[var(--accent)] hover:underline" href="/"><ShoppingBag aria-hidden="true" size={16} />Cart & checkout</Link></div></header>
    <section className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8"><Link className="inline-flex items-center gap-2 text-sm font-medium text-[var(--accent)] hover:underline" href="/products"><ArrowLeft aria-hidden="true" size={16} />Back to catalog</Link>{result.status === "not-found" ? notFound() : result.status === "unavailable" ? <Unavailable /> : <ProductDetail product={result.product} />}</section>
  </main>;
}

function ProductDetail({ product }: { product: CatalogProduct }) {
  const source = productImageSource(product.imageUrl);
  return <article className="mt-6 grid gap-8 lg:grid-cols-[minmax(0,1.15fr)_minmax(320px,0.85fr)]"><div className="grid min-h-80 place-items-center overflow-hidden border border-[var(--line)] bg-[#edf1ee] sm:min-h-120">{source ? <img alt={product.name} className="h-full max-h-[34rem] w-full object-contain p-8" referrerPolicy="no-referrer" src={source} /> : <div aria-hidden="true" className="grid place-items-center text-[var(--muted)]"><Box size={52} strokeWidth={1.25} /></div>}</div><div className="self-start border border-[var(--line)] bg-white p-6 sm:p-8">{product.category ? <p className="text-sm font-medium text-[var(--accent)]">{product.category}</p> : null}{product.brand ? <p className="mt-2 text-sm text-[var(--muted)]">{product.brand}</p> : null}<h1 className="mt-2 text-3xl font-semibold leading-tight sm:text-4xl">{product.name}</h1><p className="mt-5 text-base leading-7 text-[var(--muted)]">{product.description || "No product description is available."}</p><div className="mt-8 border-y border-[var(--line)] py-5"><p className="text-2xl font-semibold">{money.format(product.price)}</p><p className="mt-2 text-sm text-[var(--muted)]">{product.stockQuantity > 0 ? `${product.stockQuantity} currently available` : "Currently out of stock"}</p></div><div className="mt-6"><ProductPurchaseActions product={product} /></div>{product.sku ? <p className="mt-5 text-xs text-[var(--muted)]">SKU: {product.sku}</p> : null}<p className="mt-3 text-xs leading-5 text-[var(--muted)]">Price and availability are current catalog values. They are verified again when you create an order.</p></div></article>;
}

function Unavailable() { return <div className="mt-8 border border-[#f3c5c1] bg-[#fff7f6] p-5"><h1 className="font-semibold">Product details are temporarily unavailable</h1><p className="mt-1 text-sm text-[var(--muted)]">Please retry shortly. No cached or sample product is shown in its place.</p></div>; }
