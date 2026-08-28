import "server-only";

import { gatewayUrl } from "@/lib/gateway/server";
import type { CatalogProduct } from "@/lib/gateway/catalog";

export type CatalogSort = "name_asc" | "name_desc" | "price_asc" | "price_desc";

export type CatalogDiscovery = {
  items: CatalogProduct[];
  nextCursor: string | null;
  pageSize: number;
  sort: CatalogSort;
};

export type CatalogProductLookup =
  | { status: "ready"; product: CatalogProduct }
  | { status: "not-found" }
  | { status: "unavailable" };

type DiscoveryInput = {
  keyword?: string;
  category?: string;
  sort?: CatalogSort;
  cursor?: string;
  pageSize?: number;
};

export async function getCatalogDiscovery(input: DiscoveryInput = {}): Promise<CatalogDiscovery | null> {
  const query = new URLSearchParams();
  if (input.keyword?.trim()) query.set("keyword", input.keyword.trim());
  if (input.category?.trim()) query.set("category", input.category.trim());
  if (input.sort) query.set("sort", input.sort);
  if (input.cursor) query.set("cursor", input.cursor);
  if (input.pageSize) query.set("pageSize", input.pageSize.toString());

  try {
    const response = await fetch(gatewayUrl(`/catalog/products/discovery${query.size ? `?${query}` : ""}`), {
      cache: "no-store",
      headers: { Accept: "application/json" },
    });

    if (!response.ok) return null;
    return parseCatalogDiscovery(await response.json());
  } catch {
    return null;
  }
}

export async function getCatalogProduct(productId: string): Promise<CatalogProductLookup> {
  try {
    const response = await fetch(gatewayUrl(`/catalog/products/${encodeURIComponent(productId)}`), {
      cache: "no-store",
      headers: { Accept: "application/json" },
    });

    if (response.status === 404) return { status: "not-found" };
    if (!response.ok) return { status: "unavailable" };

    const product = parseCatalogProduct(await response.json());
    return product ? { status: "ready", product } : { status: "unavailable" };
  } catch {
    return { status: "unavailable" };
  }
}

function parseCatalogDiscovery(value: unknown): CatalogDiscovery | null {
  if (typeof value !== "object" || value === null) return null;
  const payload = value as Record<string, unknown>;
  const items = Array.isArray(payload.items) ? payload.items.map(parseCatalogProduct).filter((item): item is CatalogProduct => item !== null) : null;
  const nextCursor = payload.nextCursor;
  const pageSize = payload.pageSize;
  const sort = payload.sort;

  if (!items || (typeof nextCursor !== "string" && nextCursor !== null) || typeof pageSize !== "number" || !isCatalogSort(sort)) {
    return null;
  }

  return { items, nextCursor, pageSize, sort };
}

function parseCatalogProduct(value: unknown): CatalogProduct | null {
  if (typeof value !== "object" || value === null) return null;
  const product = value as Record<string, unknown>;

  if (typeof product.id !== "string" || typeof product.name !== "string" || typeof product.description !== "string" || typeof product.price !== "number" || typeof product.stockQuantity !== "number") {
    return null;
  }

  if (product.category !== undefined && product.category !== null && typeof product.category !== "string") return null;
  if (product.imageUrl !== undefined && product.imageUrl !== null && typeof product.imageUrl !== "string") return null;
  if (product.sku !== undefined && product.sku !== null && typeof product.sku !== "string") return null;
  if (product.brand !== undefined && product.brand !== null && typeof product.brand !== "string") return null;

  return {
    id: product.id,
    name: product.name,
    description: product.description,
    price: product.price,
    stockQuantity: product.stockQuantity,
    category: product.category as string | null | undefined,
    imageUrl: product.imageUrl as string | null | undefined,
    sku: product.sku as string | null | undefined,
    brand: product.brand as string | null | undefined,
  };
}

function isCatalogSort(value: unknown): value is CatalogSort {
  return value === "name_asc" || value === "name_desc" || value === "price_asc" || value === "price_desc";
}
