import Link from "next/link";
import { ArrowLeft, Box } from "lucide-react";

export default function ProductNotFound() {
  return <main className="min-h-screen bg-[var(--background)]">
    <section className="mx-auto max-w-2xl px-4 py-20 text-center sm:px-6">
      <Box aria-hidden="true" className="mx-auto text-[var(--muted)]" size={32} strokeWidth={1.5} />
      <p className="mt-6 text-sm font-medium text-[var(--accent)]">Catalog</p>
      <h1 className="mt-2 text-2xl font-semibold">Product not found</h1>
      <p className="mt-3 text-sm leading-6 text-[var(--muted)]">This product may no longer be available in the catalog.</p>
      <Link className="mt-7 inline-flex h-11 items-center gap-2 bg-[var(--accent)] px-4 text-sm font-semibold text-white hover:bg-[var(--accent-strong)]" href="/products"><ArrowLeft aria-hidden="true" size={16} />Browse products</Link>
    </section>
  </main>;
}