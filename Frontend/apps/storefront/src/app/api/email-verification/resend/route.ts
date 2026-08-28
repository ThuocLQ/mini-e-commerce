import { NextResponse } from "next/server";
import { getSessionFromRequest } from "@/app/api/session/route";
import { gatewayUrl } from "@/lib/gateway/server";
import { hasSameOrigin } from "@/lib/http/same-origin";

export async function POST(request: Request) {
  if (!hasSameOrigin(request)) return NextResponse.json({ message: "Cross-site requests are not accepted." }, { status: 403 });
  const session = await getSessionFromRequest(request);
  if (!session) return NextResponse.json({ message: "Sign in is required." }, { status: 401 });

  try {
    const response = await fetch(gatewayUrl("/auth/email-verifications/resend"), {
      method: "POST",
      headers: { Authorization: "Bearer " + session.accessToken, Accept: "application/json" },
      cache: "no-store",
    });
    if (response.status === 429) return NextResponse.json({ message: "Please wait one minute before requesting another email." }, { status: 429 });
    if (response.status === 401) return NextResponse.json({ message: "Your session has expired." }, { status: 401 });
    if (!response.ok) return NextResponse.json({ message: "Email verification is temporarily unavailable." }, { status: 503 });
    return NextResponse.json({ requested: true }, { status: 202 });
  } catch {
    return NextResponse.json({ message: "Email verification is temporarily unavailable." }, { status: 503 });
  }
}
