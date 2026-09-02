import { createServer } from "node:http";

const token = "browser-e2e-token";
const product = {
  id: "fa9dde50-d2cf-4565-92ff-e8e19df76603",
  name: "Browser test desk lamp",
  description: "Gateway-backed browser E2E fixture",
  price: 79,
  stockQuantity: 4,
  category: "Workspace",
  brand: "MicroShop Test",
  sku: "E2E-LAMP-01",
  imageUrl: null,
};
const user = {
  userId: "a2ee1f03-1677-4221-95d8-35a8155f5aa4",
  userName: "browser-e2e-user",
  role: "Customer",
  isEmailVerified: true,
  receiveOrderUpdates: true,
};

let revoked = false;
let basket = emptyBasket();

function emptyBasket() {
  return {
    basketId: "1f5ece39-0320-4053-bc5b-9f4e4ab0f940",
    userId: user.userId,
    version: 1,
    items: [],
    totalPrice: 0,
  };
}

const sendJson = (response, statusCode, payload) => {
  response.writeHead(statusCode, { "content-type": "application/json" });
  response.end(JSON.stringify(payload));
};

async function readJson(request) {
  const chunks = [];
  for await (const chunk of request) chunks.push(chunk);
  if (chunks.length === 0) return null;
  return JSON.parse(Buffer.concat(chunks).toString("utf8"));
}

createServer(async (request, response) => {
  const url = new URL(request.url ?? "/", "http://127.0.0.1:4100");
  const authorization = request.headers.authorization;
  const isAuthorized = authorization === `Bearer ${token}` && !revoked;

  if (request.method === "GET" && url.pathname === "/health") return sendJson(response, 200, { status: "ok" });
  if (request.method === "POST" && url.pathname === "/auth/login") {
    revoked = false;
    basket = emptyBasket();
    return sendJson(response, 200, { accessToken: token, expiresAt: "2099-01-01T00:00:00.000Z", tokenType: "Bearer" });
  }
  if (request.method === "POST" && url.pathname === "/auth/logout") {
    if (!isAuthorized) return sendJson(response, 401, { message: "Unauthorized" });
    revoked = true;
    response.writeHead(204);
    return response.end();
  }
  if (request.method === "GET" && url.pathname === "/auth/me") {
    return isAuthorized ? sendJson(response, 200, user) : sendJson(response, 401, { message: "Unauthorized" });
  }
  if (request.method === "GET" && url.pathname === "/catalog/products") return sendJson(response, 200, [product]);
  if (request.method === "GET" && url.pathname === `/catalog/products/${product.id}`) return sendJson(response, 200, product);

  if (url.pathname.startsWith(`/cart/${user.userId}`)) {
    if (!isAuthorized) return sendJson(response, 401, { message: "Unauthorized" });
    if (request.method === "GET") return sendJson(response, 200, basket);
    if (request.method === "POST" && url.pathname.endsWith("/items")) {
      const body = await readJson(request);
      if (body?.productId !== product.id || body.quantity !== 1) return sendJson(response, 400, { message: "Invalid cart item." });
      basket = {
        ...basket,
        version: basket.version + 1,
        items: [{ productId: product.id, productName: product.name, price: product.price, quantity: 1 }],
        totalPrice: product.price,
      };
      return sendJson(response, 200, basket);
    }
  }

  if (request.method === "GET" && url.pathname === "/me/addresses") {
    return isAuthorized ? sendJson(response, 200, []) : sendJson(response, 401, { message: "Unauthorized" });
  }

  return sendJson(response, 404, { message: `Unhandled browser E2E fixture route: ${request.method} ${url.pathname}` });
}).listen(4100, "127.0.0.1");