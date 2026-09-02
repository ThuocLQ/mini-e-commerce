import { NextResponse } from "next/server";
import { getSessionFromRequest } from "@/app/api/session/route";
import { gatewayUrl } from "@/lib/gateway/server";

export const dynamic = "force-dynamic";

export async function GET(request: Request, context: { params: Promise<{ orderId: string }> }) {
  const session = await getSessionFromRequest(request);
  if (!session) return NextResponse.json({ message: "Sign in is required." }, { status: 401 });
  const { orderId } = await context.params;
  if (!isGuid(orderId)) return NextResponse.json({ message: "Order id is invalid." }, { status: 400 });
  try {
    const upstream = await fetch(gatewayUrl(`/orders/${encodeURIComponent(orderId)}/shipment`), { headers: { Authorization: `Bearer ${session.accessToken}`, Accept: "application/json" }, cache: "no-store" });
    return new NextResponse(await upstream.text(), { status: upstream.status, headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" } });
  } catch { return NextResponse.json({ message: "Shipment tracking is unavailable. Try again shortly." }, { status: 503 }); }
}
function isGuid(value: string) { return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value); }