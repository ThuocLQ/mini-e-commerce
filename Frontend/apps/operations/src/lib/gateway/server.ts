import "server-only";

export function gatewayUrl(path: string): string {
  const baseUrl = process.env.MICROSHOP_GATEWAY_BASE_URL ?? "http://localhost:5027";
  return `${baseUrl.replace(/\/$/, "")}${path}`;
}