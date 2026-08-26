import { NextResponse } from "next/server";
import { getAdminSession } from "@/app/api/session/route";
import { gatewayUrl } from "@/lib/gateway/server";
import { hasSameOrigin } from "@/lib/http/same-origin";

type Context = { params: Promise<{ path: string[] }> };
export const dynamic = "force-dynamic";

export async function GET(request: Request, context: Context) { return proxy(request, context, "GET"); }
export async function POST(request: Request, context: Context) {
  if (!hasSameOrigin(request)) return NextResponse.json({ message: "Cross-site requests are not accepted." }, { status: 403 });
  return proxy(request, context, "POST");
}

async function proxy(request: Request, context: Context, method: "GET" | "POST") {
  const session = await getAdminSession(request);
  if (!session) return NextResponse.json({ message: "Administrator access is required." }, { status: 403 });
  const { path } = await context.params;
  try {
    const upstream = await fetch(gatewayUrl(`/procurement/${path.map(encodeURIComponent).join("/")}${new URL(request.url).search}`), {
      method,
      headers: { Authorization: `Bearer ${session.token}`, Accept: "application/json", ...(method === "POST" ? { "Content-Type": "application/json" } : {}) },
      body: method === "POST" ? await request.text() : undefined,
      cache: "no-store",
    });
    return new NextResponse(await upstream.text(), { status: upstream.status, headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" } });
  } catch { return NextResponse.json({ message: "Procurement is unavailable. Try again shortly." }, { status: 503 }); }
}