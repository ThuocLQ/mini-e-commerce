import { NextResponse } from "next/server";
import { getAdminSession } from "@/app/api/session/route";
import { gatewayUrl } from "@/lib/gateway/server";
import { hasSameOrigin } from "@/lib/http/same-origin";

export const dynamic = "force-dynamic";

export async function GET(request: Request) { return proxy(request, "GET"); }
export async function POST(request: Request) {
  if (!hasSameOrigin(request)) return NextResponse.json({ message: "Cross-site requests are not accepted." }, { status: 403 });
  return proxy(request, "POST");
}

async function proxy(request: Request, method: "GET" | "POST") {
  const session = await getAdminSession(request);
  if (!session) return NextResponse.json({ message: "Administrator access is required." }, { status: 403 });
  try {
    const upstream = await fetch(gatewayUrl("/suppliers"), {
      method,
      headers: { Authorization: `Bearer ${session.token}`, Accept: "application/json", ...(method === "POST" ? { "Content-Type": "application/json" } : {}) },
      body: method === "POST" ? await request.text() : undefined,
      cache: "no-store",
    });
    return new NextResponse(await upstream.text(), { status: upstream.status, headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" } });
  } catch { return NextResponse.json({ message: "Supplier workspace is unavailable. Try again shortly." }, { status: 503 }); }
}