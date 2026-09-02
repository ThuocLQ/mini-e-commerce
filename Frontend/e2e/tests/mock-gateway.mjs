import { createServer } from "node:http";

const token = "browser-e2e-token";
const products = [
  { id: "fa9dde50-d2cf-4565-92ff-e8e19df76603", name: "Browser test desk lamp", description: "Gateway-backed browser E2E fixture with an intentionally concise product description.", price: 79, stockQuantity: 4, category: "Workspace", brand: "MicroShop Test", sku: "E2E-LAMP-01", imageUrl: "https://images.unsplash.com/photo-1507473885765-e6ed057f782c?auto=format&fit=crop&w=800&q=80" },
  { id: "c4cafc64-cefd-4554-9503-9d790ffdf95c", name: "A very long product name that must not push actions out of alignment", description: "A deliberately longer fixture description checks that product card actions remain aligned even when content varies across the catalog.", price: 129, stockQuantity: 8, category: "Accessories", brand: "MicroShop Test", sku: "E2E-LONG-01", imageUrl: "https://images.unsplash.com/photo-1625948515291-69613efd103f?auto=format&fit=crop&w=800&q=80" },
  { id: "4d46471a-4f1c-4c7a-96c9-c2e013012ae8", name: "Media fallback sample", description: "This fixture has a broken remote image so the customer always receives an honest, stable media fallback.", price: 49, stockQuantity: 12, category: "Audio", brand: "MicroShop Test", sku: "E2E-FALLBACK-01", imageUrl: "https://media.test/broken-product.webp" },
  { id: "c2986cc6-9f51-4cef-a5ee-8303cc7bf48c", name: "Compact travel organizer", description: "A compact travel item makes the category selector and responsive product grid exercise varied catalog content.", price: 36, stockQuantity: 18, category: "Travel", brand: "MicroShop Test", sku: "E2E-TRAVEL-01", imageUrl: "https://images.unsplash.com/photo-1553062407-98eeb64c6a62?auto=format&fit=crop&w=800&q=80" },
];
const primaryProduct = products[0];
const user = { userId: "a2ee1f03-1677-4221-95d8-35a8155f5aa4", userName: "browser-e2e-user", role: "Customer", isEmailVerified: true, receiveOrderUpdates: true };
let revoked = false;
let basket = emptyBasket();

function emptyBasket() { return { basketId: "1f5ece39-0320-4053-bc5b-9f4e4ab0f940", userId: user.userId, version: 1, items: [], totalPrice: 0 }; }
function sendJson(response, statusCode, payload) { response.writeHead(statusCode, { "content-type": "application/json" }); response.end(JSON.stringify(payload)); }
async function readJson(request) { const chunks = []; for await (const chunk of request) chunks.push(chunk); return chunks.length === 0 ? null : JSON.parse(Buffer.concat(chunks).toString("utf8")); }
function discovery(url) {
  const category = url.searchParams.get("category");
  const keyword = url.searchParams.get("keyword")?.toLowerCase();
  const items = products.filter((item) => (!category || item.category === category) && (!keyword || `${item.name} ${item.description}`.toLowerCase().includes(keyword)));
  return { items, nextCursor: null, pageSize: Number(url.searchParams.get("pageSize") ?? items.length), sort: url.searchParams.get("sort") ?? "name_asc" };
}

createServer(async (request, response) => {
  const url = new URL(request.url ?? "/", "http://127.0.0.1:4100");
  const isAuthorized = request.headers.authorization === `Bearer ${token}` && !revoked;
  if (request.method === "GET" && url.pathname === "/health") return sendJson(response, 200, { status: "ok" });
  if (request.method === "POST" && url.pathname === "/auth/login") { revoked = false; basket = emptyBasket(); return sendJson(response, 200, { accessToken: token, expiresAt: "2099-01-01T00:00:00.000Z", tokenType: "Bearer" }); }
  if (request.method === "POST" && url.pathname === "/auth/logout") { if (!isAuthorized) return sendJson(response, 401, { message: "Unauthorized" }); revoked = true; response.writeHead(204); return response.end(); }
  if (request.method === "GET" && url.pathname === "/auth/me") return isAuthorized ? sendJson(response, 200, user) : sendJson(response, 401, { message: "Unauthorized" });
  if (request.method === "GET" && url.pathname === "/catalog/products/discovery") return sendJson(response, 200, discovery(url));
  if (request.method === "GET" && url.pathname === "/catalog/products/search") return sendJson(response, 200, discovery(new URL(`/catalog/products/discovery?keyword=${encodeURIComponent(url.searchParams.get("keyword") ?? "")}`, "http://127.0.0.1:4100")).items);
  if (request.method === "GET" && url.pathname === "/catalog/products") return sendJson(response, 200, products);
  if (request.method === "GET" && url.pathname.startsWith("/catalog/products/")) { const product = products.find((item) => item.id === url.pathname.split("/").at(-1)); return product ? sendJson(response, 200, product) : sendJson(response, 404, { message: "Not found" }); }
  if (url.pathname.startsWith(`/cart/${user.userId}`)) {
    if (!isAuthorized) return sendJson(response, 401, { message: "Unauthorized" });
    if (request.method === "GET") return sendJson(response, 200, basket);
    if (request.method === "POST" && url.pathname.endsWith("/items")) { const body = await readJson(request); if (body?.productId !== primaryProduct.id || body.quantity !== 1) return sendJson(response, 400, { message: "Invalid cart item." }); basket = { ...basket, version: basket.version + 1, items: [{ productId: primaryProduct.id, productName: primaryProduct.name, price: primaryProduct.price, quantity: 1 }], totalPrice: primaryProduct.price }; return sendJson(response, 200, basket); }
  }
  if (request.method === "GET" && url.pathname === "/me/addresses") return isAuthorized ? sendJson(response, 200, []) : sendJson(response, 401, { message: "Unauthorized" });
  return sendJson(response, 404, { message: `Unhandled browser E2E fixture route: ${request.method} ${url.pathname}` });
}).listen(4100, "127.0.0.1");