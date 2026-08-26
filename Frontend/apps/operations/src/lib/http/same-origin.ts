export function hasSameOrigin(request: Request): boolean {
  const origin = request.headers.get("origin");
  if (origin === null) return true;

  const configuredOrigin = process.env.MICROSHOP_PUBLIC_ORIGIN?.replace(/\/$/, "");
  if (configuredOrigin) return origin === configuredOrigin;

  return origin === new URL(request.url).origin;
}