import { NextResponse } from "next/server";
import { getAdminSession } from "@/app/api/session/route";
import { gatewayUrl } from "@/lib/gateway/server";
import { hasSameOrigin } from "@/lib/http/same-origin";

export const dynamic = "force-dynamic";
type Context = { params: Promise<{ path: string[] }> };
export const GET = (request: Request, context: Context) => proxy(request, context);
export const POST = (request: Request, context: Context) => proxy(request, context);
export const PUT = (request: Request, context: Context) => proxy(request, context);

async function proxy(request: Request, context: Context) {
  if (request.method !== "GET" && !hasSameOrigin(request)) return NextResponse.json({ message: "Cross-site requests are not accepted." }, { status: 403 });
  const session = await getAdminSession(request);
  if (!session) return NextResponse.json({ message: "Administrator access is required." }, { status: 403 });
  const { path } = await context.params;
  const body = request.method === "GET" ? undefined : await request.text();
  try {
    const upstream = await fetch(gatewayUrl(`/catalog/${path.map(encodeURIComponent).join("/")}${new URL(request.url).search}`), { method: request.method, headers: { Authorization: `Bearer ${session.token}`, Accept: "application/json", ...(body ? { "Content-Type": request.headers.get("content-type") ?? "application/json" } : {}) }, body, cache: "no-store" });
    return new NextResponse(await upstream.text(), { status: upstream.status, headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" } });
  } catch { return NextResponse.json({ message: "Catalog is unavailable. Try again shortly." }, { status: 503 }); }
}