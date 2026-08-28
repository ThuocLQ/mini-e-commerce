export type CatalogProduct = {
  id: string;
  name: string;
  description: string;
  price: number;
  stockQuantity: number;
  category?: string | null;
  imageUrl?: string | null;
  sku?: string | null;
  brand?: string | null;
};

type CatalogRequest = {
  query?: string;
  signal?: AbortSignal;
};

export async function getCatalogProducts({ query, signal }: CatalogRequest = {}): Promise<CatalogProduct[]> {
  const keyword = query?.trim();
  const endpoint = keyword
    ? `/api/catalog/products/search?keyword=${encodeURIComponent(keyword)}`
    : "/api/catalog/products";
  const response = await fetch(endpoint, {
    signal,
    headers: { Accept: "application/json" },
  });
  if (!response.ok) throw new Error("CATALOG_UNAVAILABLE");

  const payload: unknown = await response.json();
  if (!Array.isArray(payload)) throw new Error("CATALOG_UNAVAILABLE");

  return payload.filter(isCatalogProduct);
}

function isCatalogProduct(value: unknown): value is CatalogProduct {
  if (typeof value !== "object" || value === null) return false;
  const product = value as Record<string, unknown>;
  return typeof product.id === "string"
    && typeof product.name === "string"
    && typeof product.description === "string"
    && typeof product.price === "number"
    && typeof product.stockQuantity === "number"
    && (product.category === undefined || product.category === null || typeof product.category === "string")
    && (product.imageUrl === undefined || product.imageUrl === null || typeof product.imageUrl === "string")
    && (product.sku === undefined || product.sku === null || typeof product.sku === "string")
    && (product.brand === undefined || product.brand === null || typeof product.brand === "string");
}