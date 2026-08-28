import { NextResponse } from "next/server";
import { gatewayUrl } from "@/lib/gateway/server";
import { hasSameOrigin } from "@/lib/http/same-origin";

export async function POST(request: Request) {
  if (!hasSameOrigin(request)) return NextResponse.json({ message: "Cross-site requests are not accepted." }, { status: 403 });

  const body = await request.json().catch(() => null);
  const token = typeof body === "object" && body !== null && typeof (body as { token?: unknown }).token === "string"
    ? (body as { token: string }).token.trim()
    : "";

  if (!token || token.length > 256) return NextResponse.json({ message: "Verification link is invalid." }, { status: 400 });

  try {
    const response = await fetch(gatewayUrl("/auth/email-verifications"), {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({ token }),
      cache: "no-store",
    });

    if (!response.ok) return NextResponse.json({ message: "This verification link is invalid or expired." }, { status: 400 });
    return NextResponse.json({ verified: true });
  } catch {
    return NextResponse.json({ message: "Email verification is temporarily unavailable." }, { status: 503 });
  }
}
