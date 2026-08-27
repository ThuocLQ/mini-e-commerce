import { NextResponse } from "next/server";
import { getSessionFromRequest } from "@/app/api/session/route";
import { gatewayUrl } from "@/lib/gateway/server";
import { hasSameOrigin } from "@/lib/http/same-origin";

export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  if (!hasSameOrigin(request)) return NextResponse.json({ message: "Cross-site requests are not accepted." }, { status: 403 });
  const session = await getSessionFromRequest(request);
  if (!session) return NextResponse.json({ message: "Sign in is required." }, { status: 401 });

  const idempotencyKey = request.headers.get("idempotency-key")?.trim();
  if (!idempotencyKey) return NextResponse.json({ message: "A payment action idempotency key is required." }, { status: 400 });

  const body = await request.text();
  try {
    const upstream = await fetch(gatewayUrl("/payments"), {
      method: "POST",
      headers: {
        Authorization: "Bearer " + session.accessToken,
        Accept: "application/json",
        "Content-Type": request.headers.get("content-type") ?? "application/json",
        "Idempotency-Key": idempotencyKey,
      },
      body,
      cache: "no-store",
    });
    return new NextResponse(await upstream.text(), { status: upstream.status, headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" } });
  } catch {
    return NextResponse.json({ message: "Payments are unavailable. Please try again." }, { status: 503 });
  }
}
