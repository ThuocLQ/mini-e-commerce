import { NextResponse } from "next/server";
import { getAdminSession } from "@/app/api/session/route";
import { gatewayUrl } from "@/lib/gateway/server";

type Context = { params: Promise<{ path: string[] }> };
export const dynamic = "force-dynamic";

export async function GET(request: Request, context: Context) {
  const session = await getAdminSession(request);
  if (!session) return NextResponse.json({ message: "Administrator access is required." }, { status: 403 });

  const { path } = await context.params;
  try {
    const upstream = await fetch(gatewayUrl(`/payments/${path.map(encodeURIComponent).join("/")}${new URL(request.url).search}`), {
      headers: { Authorization: `Bearer ${session.token}`, Accept: "application/json" },
      cache: "no-store",
    });
    return new NextResponse(await upstream.text(), {
      status: upstream.status,
      headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" },
    });
  } catch {
    return NextResponse.json({ message: "Payments are unavailable. Try again shortly." }, { status: 503 });
  }
}
