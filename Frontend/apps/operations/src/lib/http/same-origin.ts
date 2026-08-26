export function hasSameOrigin(request: Request): boolean {
  const origin = request.headers.get("origin");
  if (origin === null) return false;

  const configuredOrigins = getConfiguredOrigins();
  if (configuredOrigins !== null) return configuredOrigins.includes(origin);

  try {
    return origin === new URL(request.url).origin;
  } catch {
    return false;
  }
}

function getConfiguredOrigins(): string[] | null {
  const configured = process.env.MICROSHOP_ALLOWED_ORIGINS
    ?? process.env.MICROSHOP_PUBLIC_ORIGIN;

  if (configured === undefined) return null;

  return configured
    .split(",")
    .map(value => value.trim())
    .filter(Boolean)
    .flatMap(normalizeOrigin);
}

function normalizeOrigin(value: string): string[] {
  try {
    return [new URL(value).origin];
  } catch {
    return [];
  }
}
