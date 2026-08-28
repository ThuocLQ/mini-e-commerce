import { NextResponse } from "next/server";
import { getSessionFromRequest } from "@/app/api/session/route";
import { gatewayUrl } from "@/lib/gateway/server";
import { hasSameOrigin } from "@/lib/http/same-origin";

export const dynamic = "force-dynamic";

type CheckoutRequest = {
  basketId: string;
  basketVersion: number;
  shippingAddressId: string;
  couponCode?: string;
  idempotencyKey: string;
  quoteToken?: string;
};

export async function POST(request: Request) {
  if (!hasSameOrigin(request)) return NextResponse.json({ message: "Cross-site requests are not accepted." }, { status: 403 });
  const session = await getSessionFromRequest(request);
  if (!session) {
    return NextResponse.json({ message: "Sign in is required." }, { status: 401 });
  }

  const body = await readJson(request);
  if (!isCheckoutRequest(body)) {
    return NextResponse.json({ message: "Basket information is missing or invalid." }, { status: 400 });
  }

  try {
    const upstream = await fetch(gatewayUrl("/orders/checkout"), {
      method: "POST",
      headers: {
        Authorization: `Bearer ${session.accessToken}`,
        "Idempotency-Key": body.idempotencyKey,
        "Content-Type": "application/json",
        Accept: "application/json",
      },
      body: JSON.stringify({
        basketId: body.basketId,
        basketVersion: body.basketVersion,
        shippingAddressId: body.shippingAddressId,
        couponCode: body.couponCode?.trim() || null,
        quoteToken: body.quoteToken?.trim() || null,
      }),
      cache: "no-store",
    });

    return new NextResponse(await upstream.text(), {
      status: upstream.status,
      headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" },
    });
  } catch {
    return NextResponse.json({ message: "Checkout is unavailable. Please try again." }, { status: 503 });
  }
}

async function readJson(request: Request): Promise<unknown> {
  try {
    return await request.json();
  } catch {
    return null;
  }
}

function isCheckoutRequest(value: unknown): value is CheckoutRequest {
  if (typeof value !== "object" || value === null) return false;
  const body = value as Record<string, unknown>;
  return typeof body.basketId === "string"
    && typeof body.basketVersion === "number"
    && body.basketVersion > 0
    && isGuid(body.shippingAddressId)
    && typeof body.idempotencyKey === "string"
    && body.idempotencyKey.length > 0
    && body.idempotencyKey.length <= 128
    && (body.couponCode === undefined || typeof body.couponCode === "string")
    && (body.quoteToken === undefined || (typeof body.quoteToken === "string" && body.quoteToken.length <= 4096));
}

function isGuid(value: unknown): value is string {
  return typeof value === "string" && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}
