import { NextResponse } from "next/server";
import { getSessionFromRequest } from "@/app/api/session/route";
import { gatewayUrl } from "@/lib/gateway/server";
import { hasSameOrigin } from "@/lib/http/same-origin";

export const dynamic = "force-dynamic";

export async function POST(request: Request, context: { params: Promise<{ orderId: string }> }) {
  if (!hasSameOrigin(request)) {
    return NextResponse.json({ message: "Cross-site requests are not accepted." }, { status: 403 });
  }

  const session = await getSessionFromRequest(request);
  if (!session) {
    return NextResponse.json({ message: "Sign in is required." }, { status: 401 });
  }

  const { orderId } = await context.params;
  if (!isGuid(orderId)) {
    return NextResponse.json({ message: "Order id is invalid." }, { status: 400 });
  }

  let body: { reason?: unknown } | null = null;
  try {
    body = await request.json();
  } catch {
    // An empty cancellation body is valid; the ordering workflow records its default reason.
  }

  if (body?.reason !== undefined && typeof body.reason !== "string") {
    return NextResponse.json({ message: "Cancellation reason is invalid." }, { status: 400 });
  }

  try {
    const upstream = await fetch(gatewayUrl(`/orders/${encodeURIComponent(orderId)}/cancel`), {
      method: "POST",
      headers: {
        Authorization: `Bearer ${session.accessToken}`,
        Accept: "application/json",
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ reason: body?.reason }),
      cache: "no-store",
    });

    return new NextResponse(await upstream.text(), {
      status: upstream.status,
      headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" },
    });
  } catch {
    return NextResponse.json({ message: "Order cancellation is unavailable. Please try again." }, { status: 503 });
  }
}

function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}