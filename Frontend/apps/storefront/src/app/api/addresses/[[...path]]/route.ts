import { NextResponse } from "next/server";
import { getSessionFromRequest } from "@/app/api/session/route";
import { gatewayUrl } from "@/lib/gateway/server";
import { hasSameOrigin } from "@/lib/http/same-origin";

export const dynamic = "force-dynamic";

type RouteContext = { params: Promise<{ path?: string[] }> };

export async function GET(request: Request, context: RouteContext) { return proxyAddresses(request, context); }
export async function POST(request: Request, context: RouteContext) { return proxyAddresses(request, context); }
export async function PATCH(request: Request, context: RouteContext) { return proxyAddresses(request, context); }
export async function PUT(request: Request, context: RouteContext) { return proxyAddresses(request, context); }
export async function DELETE(request: Request, context: RouteContext) { return proxyAddresses(request, context); }

async function proxyAddresses(request: Request, context: RouteContext) {
  if (request.method !== "GET" && !hasSameOrigin(request)) return NextResponse.json({ message: "Cross-site requests are not accepted." }, { status: 403 });

  const session = await getSessionFromRequest(request);
  if (!session) return NextResponse.json({ message: "Sign in is required." }, { status: 401 });

  const { path = [] } = await context.params;
  if (!isAllowedPath(path)) return NextResponse.json({ message: "The requested address action is not available." }, { status: 404 });

  const body = request.method === "GET" ? undefined : await request.text();
  const headers: Record<string, string> = { Authorization: `Bearer ${session.accessToken}`, Accept: "application/json" };
  if (body) headers["Content-Type"] = request.headers.get("content-type") ?? "application/json";
  if (request.method === "POST") {
    const idempotencyKey = request.headers.get("Idempotency-Key");
    if (idempotencyKey) headers["Idempotency-Key"] = idempotencyKey;
  }

  try {
    const upstream = await fetch(gatewayUrl(`/me/addresses${path.length ? `/${path.map(encodeURIComponent).join("/")}` : ""}`), {
      method: request.method,
      headers,
      body,
      cache: "no-store",
    });
    if (upstream.status === 204) {
      return new NextResponse(null, { status: upstream.status });
    }
    return new NextResponse(await upstream.text(), { status: upstream.status, headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" } });
  } catch {
    return NextResponse.json({ message: "Addresses are unavailable. Please try again." }, { status: 503 });
  }
}

function isAllowedPath(path: string[]) {
  return path.length === 0 || (path.length === 1 && isGuid(path[0])) || (path.length === 2 && isGuid(path[0]) && path[1] === "default");
}

function isGuid(value: string) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}
