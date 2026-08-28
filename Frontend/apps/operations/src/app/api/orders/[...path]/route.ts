import { NextResponse } from "next/server";
import { getAdminSession } from "@/app/api/session/route";
import { gatewayUrl } from "@/lib/gateway/server";
import { hasSameOrigin } from "@/lib/http/same-origin";

type Context = { params: Promise<{ path: string[] }> };
export const dynamic = "force-dynamic";

export async function GET(request: Request, context: Context) {
  const session = await getAdminSession(request);
  if (!session) return NextResponse.json({ message: "Administrator access is required." }, { status: 403 });
  const { path } = await context.params;
  try {
    const upstream = await fetch(gatewayUrl(`/orders/${path.map(encodeURIComponent).join("/")}${new URL(request.url).search}`), {
      headers: { Authorization: `Bearer ${session.token}`, Accept: "application/json" },
      cache: "no-store",
    });
    return new NextResponse(await upstream.text(), { status: upstream.status, headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" } });
  } catch { return NextResponse.json({ message: "Orders are unavailable. Try again shortly." }, { status: 503 }); }
}
export async function POST(request: Request, context: Context) {
  if (!hasSameOrigin(request)) return NextResponse.json({ message: "Cross-site requests are not accepted." }, { status: 403 });
  const session = await getAdminSession(request);
  if (!session) return NextResponse.json({ message: "Administrator access is required." }, { status: 403 });

  const { path } = await context.params;
  try {
    const upstream = await fetch(gatewayUrl(`/orders/${path.map(encodeURIComponent).join("/")}`), {
      method: "POST",
      headers: {
        Authorization: `Bearer ${session.token}`,
        Accept: "application/json",
        "Content-Type": request.headers.get("content-type") ?? "application/json",
      },
      body: await request.text(),
      cache: "no-store",
    });
    return new NextResponse(await upstream.text(), { status: upstream.status, headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" } });
  } catch { return NextResponse.json({ message: "Order operation is unavailable. Try again shortly." }, { status: 503 }); }
}