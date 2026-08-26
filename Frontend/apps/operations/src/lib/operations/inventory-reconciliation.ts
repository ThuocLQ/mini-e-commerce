export type InventoryItem = {
  productId: string;
  stockQuantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  updatedAtUtc: string;
};

export type Product = { id: string; name: string; description: string };
export type ReconciliationState = "balanced" | "quantityMismatch" | "invalidQuantity" | "missingCatalog";
export type InventoryRow = InventoryItem & { name: string; description: string; reconciliation: ReconciliationState };
export type InventoryReconciliation = { rows: InventoryRow[]; productsMissingInventory: Product[] };

export async function loadInventoryReconciliation(signal?: AbortSignal): Promise<InventoryReconciliation> {
  const [inventoryResponse, catalogResponse] = await Promise.all([
    fetch("/api/inventory/admin/items", { signal }),
    fetch("/api/catalog/products", { signal }),
  ]);
  const [inventory, catalog] = await Promise.all([
    inventoryResponse.json().catch(() => null),
    catalogResponse.json().catch(() => null),
  ]);

  if (!inventoryResponse.ok || !isInventoryList(inventory)) throw new OperationsApiError(inventoryResponse.status, messageOf(inventory) ?? "Inventory could not be loaded.");
  if (!catalogResponse.ok || !isProductList(catalog)) throw new OperationsApiError(catalogResponse.status, messageOf(catalog) ?? "Catalog products could not be loaded.");

  const productById = new Map(catalog.map((product) => [product.id, product]));
  const inventoryProductIds = new Set(inventory.map((item) => item.productId));
  return {
    rows: inventory.map((item) => {
      const product = productById.get(item.productId);
      return { ...item, name: product?.name ?? "Catalog item not found", description: product?.description ?? item.productId, reconciliation: reconciliationOf(item, product) };
    }),
    productsMissingInventory: catalog.filter((product) => !inventoryProductIds.has(product.id)),
  };
}

export class OperationsApiError extends Error {
  constructor(readonly status: number, message: string) { super(message); }
}

function reconciliationOf(item: InventoryItem, product: Product | undefined): ReconciliationState {
  if (!product) return "missingCatalog";
  if (item.stockQuantity < 0 || item.reservedQuantity < 0 || item.availableQuantity < 0 || item.reservedQuantity > item.stockQuantity) return "invalidQuantity";
  return item.availableQuantity === item.stockQuantity - item.reservedQuantity ? "balanced" : "quantityMismatch";
}

function isInventoryList(value: unknown): value is InventoryItem[] {
  return Array.isArray(value) && value.every((item) => isRecord(item) && typeof item.productId === "string" && typeof item.stockQuantity === "number" && typeof item.reservedQuantity === "number" && typeof item.availableQuantity === "number" && typeof item.updatedAtUtc === "string");
}

function isProductList(value: unknown): value is Product[] {
  return Array.isArray(value) && value.every((item) => isRecord(item) && typeof item.id === "string" && typeof item.name === "string" && typeof item.description === "string");
}

function messageOf(value: unknown): string | null { return isRecord(value) && typeof value.message === "string" ? value.message : null; }
function isRecord(value: unknown): value is Record<string, unknown> { return typeof value === "object" && value !== null; }
