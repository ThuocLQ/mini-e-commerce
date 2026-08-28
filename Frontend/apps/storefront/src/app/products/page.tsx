import Link from "next/link";
import { ArrowLeft, ArrowRight, Box, Search } from "lucide-react";
import { getCatalogDiscovery, type CatalogSort } from "@/lib/gateway/catalog-server";
import { productImageSource } from "@/lib/storefront/product-media";

const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });
const validSorts = new Set<CatalogSort>(["name_asc", "name_desc", "price_asc", "price_desc"]);

type SearchParams = Promise<{ keyword?: string; category?: string; sort?: string; cursor?: string }>;

export default async function ProductsPage({ searchParams }: { searchParams: SearchParams }) {
  const params = await searchParams;
  const keyword = params.keyword?.trim() ?? "";
  const category = params.category?.trim() ?? "";
  const sort = validSorts.has(params.sort as CatalogSort) ? params.sort as CatalogSort : "name_asc";
  const discovery = await getCatalogDiscovery({ keyword, category, sort, cursor: params.cursor, pageSize: 12 });

  return <main className="min-h-screen bg-[var(--background)]">
    <header className="border-b border-[var(--line)] bg-[var(--surface)]"><div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-4 py-3 sm:px-6 lg:px-8"><Link className="flex items-center gap-3 text-left" href="/"><span className="grid size-9 place-items-center bg-[var(--accent)] text-white"><Box aria-hidden="true" size={19} /></span><span><span className="block text-sm font-semibold">MicroShop</span><span className="block text-xs text-[var(--muted)]">Customer store</span></span></Link><Link className="text-sm font-medium text-[var(--accent)] hover:underline" href="/">Cart & checkout</Link></div></header>
    <section className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
      <Link className="inline-flex items-center gap-2 text-sm font-medium text-[var(--accent)] hover:underline" href="/"><ArrowLeft aria-hidden="true" size={16} />Back to store</Link>
      <div className="mt-5 border-b border-[var(--line)] pb-6"><p className="text-sm font-medium text-[var(--accent)]">Catalog</p><h1 className="mt-2 text-3xl font-semibold sm:text-4xl">Browse products</h1><p className="mt-2 max-w-2xl text-sm leading-6 text-[var(--muted)] sm:text-base">Availability and pricing are read from the current catalog. Your cart and checkout verify them again before an order is created.</p></div>
      <form className="mt-6 grid gap-3 border border-[var(--line)] bg-white p-4 md:grid-cols-[minmax(0,1fr)_180px_180px_auto]" method="get">
        <label className="relative block"><span className="sr-only">Search products</span><Search aria-hidden="true" className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[var(--muted)]" size={18} /><input className="h-11 w-full border border-[var(--line)] bg-white pl-10 pr-3 text-sm outline-none focus:border-[var(--accent)]" defaultValue={keyword} name="keyword" placeholder="Search name or description" type="search" /></label>
        <label><span className="sr-only">Category</span><input className="h-11 w-full border border-[var(--line)] bg-white px-3 text-sm outline-none focus:border-[var(--accent)]" defaultValue={category} name="category" placeholder="Category" /></label>
        <label><span className="sr-only">Sort products</span><select className="h-11 w-full border border-[var(--line)] bg-white px-3 text-sm outline-none focus:border-[var(--accent)]" defaultValue={sort} name="sort"><option value="name_asc">Name: A to Z</option><option value="name_desc">Name: Z to A</option><option value="price_asc">Price: low to high</option><option value="price_desc">Price: high to low</option></select></label>
        <button className="h-11 bg-[var(--accent)] px-5 text-sm font-semibold text-white hover:bg-[var(--accent-strong)]" type="submit">Apply</button>
      </form>
      {!discovery ? <Unavailable /> : <CatalogResults category={category} discovery={discovery} keyword={keyword} sort={sort} />}
    </section>
  </main>;
}

function CatalogResults({ discovery, keyword, category, sort }: { discovery: NonNullable<Awaited<ReturnType<typeof getCatalogDiscovery>>>; keyword: string; category: string; sort: CatalogSort }) {
  if (discovery.items.length === 0) return <div className="mt-8 border border-dashed border-[var(--line)] bg-white px-6 py-14 text-center"><Box aria-hidden="true" className="mx-auto text-[var(--muted)]" size={28} /><h2 className="mt-4 text-lg font-semibold">No matching products</h2><p className="mt-2 text-sm text-[var(--muted)]">Change your search or category and try again.</p></div>;
  const next = discovery.nextCursor ? new URLSearchParams({ ...(keyword ? { keyword } : {}), ...(category ? { category } : {}), sort, cursor: discovery.nextCursor }).toString() : null;
  return <><p className="mt-6 text-sm text-[var(--muted)]" role="status">Showing up to {discovery.pageSize} current catalog products.</p><div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">{discovery.items.map((product) => <article className="flex min-h-80 flex-col border border-[var(--line)] bg-white p-4" key={product.id}><ProductMedia imageUrl={product.imageUrl} name={product.name} /><div className="mt-5 flex items-start justify-between gap-3"><h2 className="text-base font-semibold leading-6"><Link className="hover:text-[var(--accent)] hover:underline" href={`/products/${encodeURIComponent(product.id)}`}>{product.name}</Link></h2><Stock quantity={product.stockQuantity} /></div>{product.category ? <p className="mt-2 text-xs font-medium uppercase tracking-wide text-[var(--accent)]">{product.category}</p> : null}<p className="mt-2 line-clamp-2 text-sm leading-5 text-[var(--muted)]">{product.description || "No product description is available."}</p><div className="mt-auto flex items-center justify-between gap-3 pt-6"><p className="text-lg font-semibold">{money.format(product.price)}</p><Link className="inline-flex h-10 items-center gap-2 px-3 text-sm font-semibold text-[var(--accent)] hover:bg-[#e9f2ed]" href={`/products/${encodeURIComponent(product.id)}`}>View <ArrowRight aria-hidden="true" size={16} /></Link></div></article>)}</div>{next ? <div className="mt-8 flex justify-end"><Link className="inline-flex h-11 items-center gap-2 bg-[var(--accent)] px-4 text-sm font-semibold text-white hover:bg-[var(--accent-strong)]" href={`/products?${next}`}>Next products <ArrowRight aria-hidden="true" size={16} /></Link></div> : null}</>;
}

function ProductMedia({ imageUrl, name }: { imageUrl: string | null | undefined; name: string }) {
  const source = productImageSource(imageUrl);
  return source ? <div className="h-36 overflow-hidden bg-[#edf1ee]"><img alt={name} className="h-full w-full object-contain p-3" loading="lazy" referrerPolicy="no-referrer" src={source} /></div> : <div aria-hidden="true" className="grid h-36 place-items-center bg-[#eef2ef] text-[var(--muted)]"><Box size={34} strokeWidth={1.5} /></div>;
}

function Stock({ quantity }: { quantity: number }) { return quantity <= 0 ? <span className="shrink-0 border border-[#f3c5c1] bg-[#fff7f6] px-2 py-1 text-xs font-medium text-[var(--danger)]">Out of stock</span> : <span className="shrink-0 border border-[#b9d7c6] bg-[#f4fbf6] px-2 py-1 text-xs font-medium text-[var(--accent-strong)]">{quantity} in stock</span>; }
function Unavailable() { return <div className="mt-8 border border-[#f3c5c1] bg-[#fff7f6] p-5"><h2 className="font-semibold">Catalog is temporarily unavailable</h2><p className="mt-1 text-sm text-[var(--muted)]">Please retry shortly. No cached or sample products are shown in its place.</p></div>; }
