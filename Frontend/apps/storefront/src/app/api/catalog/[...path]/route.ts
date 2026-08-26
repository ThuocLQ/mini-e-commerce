import { NextResponse } from "next/server";
import { gatewayUrl } from "@/lib/gateway/server";

export const dynamic = "force-dynamic";

export async function GET(request: Request, context: { params: Promise<{ path: string[] }> }) {
  const { path } = await context.params;
  const upstreamPath = `/catalog/${path.map(encodeURIComponent).join("/")}${new URL(request.url).search}`;

  try {
    const upstream = await fetch(gatewayUrl(upstreamPath), {
      headers: { Accept: "application/json" },
      cache: "no-store",
    });
    return relay(upstream);
  } catch {
    return NextResponse.json({ message: "Catalog is unavailable." }, { status: 503 });
  }
}

async function relay(upstream: Response) {
  return new NextResponse(await upstream.text(), {
    status: upstream.status,
    headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" },
  });
}
