import { NextResponse } from "next/server";
import { getSessionFromRequest } from "@/app/api/session/route";
import { gatewayUrl } from "@/lib/gateway/server";

export const dynamic = "force-dynamic";

type Context = { params: Promise<{ paymentId: string }> };

export async function GET(request: Request, context: Context) {
  const session = await getSessionFromRequest(request);
  if (!session) return NextResponse.json({ message: "Sign in is required." }, { status: 401 });

  const { paymentId } = await context.params;
  if (!isGuid(paymentId)) return NextResponse.json({ message: "Payment id is invalid." }, { status: 400 });

  try {
    const upstream = await fetch(gatewayUrl(`/payments/${encodeURIComponent(paymentId)}`), {
      headers: { Authorization: `Bearer ${session.accessToken}`, Accept: "application/json" },
      cache: "no-store",
    });
    return new NextResponse(await upstream.text(), {
      status: upstream.status,
      headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" },
    });
  } catch {
    return NextResponse.json({ message: "Payment status is unavailable. Please try again." }, { status: 503 });
  }
}
function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}