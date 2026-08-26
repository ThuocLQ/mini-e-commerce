export function hasSameOrigin(request: Request): boolean {
  const origin = request.headers.get("origin");
  if (origin === null) return true;

  const configuredOrigin = process.env.MICROSHOP_PUBLIC_ORIGIN?.replace(/\/$/, "");
  if (configuredOrigin && origin === configuredOrigin) return true;

  if (process.env.MICROSHOP_ALLOW_TRYCLOUDFLARE_ORIGIN === "true") {
    try {
      const publicOrigin = new URL(origin);
      return publicOrigin.protocol === "https:" && publicOrigin.hostname.endsWith(".trycloudflare.com");
    } catch {
      return false;
    }
  }

  return !configuredOrigin && origin === new URL(request.url).origin;
}