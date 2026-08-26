import "server-only";

export function gatewayUrl(path: string): string {
  const configuredUrl = process.env.MICROSHOP_GATEWAY_BASE_URL;

  if (!configuredUrl && process.env.NODE_ENV === "production") {
    throw new Error("MICROSHOP_GATEWAY_BASE_URL is required in production.");
  }

  const baseUrl = configuredUrl ?? "http://localhost:5027";
  return baseUrl.endsWith("/") ? baseUrl.slice(0, -1) + path : baseUrl + path;
}
