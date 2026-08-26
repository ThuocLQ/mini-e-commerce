import { NextResponse } from "next/server";
import { getSessionFromRequest } from "@/app/api/session/route";
import { gatewayUrl } from "@/lib/gateway/server";
import { hasSameOrigin } from "@/lib/http/same-origin";

export const dynamic = "force-dynamic";

export async function GET(request: Request, context: RouteContext) {
  return proxyCart(request, context);
}

export async function POST(request: Request, context: RouteContext) {
  return proxyCart(request, context);
}

export async function PUT(request: Request, context: RouteContext) {
  return proxyCart(request, context);
}

export async function DELETE(request: Request, context: RouteContext) {
  return proxyCart(request, context);
}

type RouteContext = { params: Promise<{ path: string[] }> };

async function proxyCart(request: Request, context: RouteContext) {
  if (request.method !== "GET" && !hasSameOrigin(request)) {
    return NextResponse.json({ message: "Cross-site requests are not accepted." }, { status: 403 });
  }
  const session = await getSessionFromRequest(request);
  if (!session) {
    return NextResponse.json({ message: "Sign in is required." }, { status: 401 });
  }

  const { path } = await context.params;
  if (path.length === 0 || path[0] !== session.user.userId) {
    return NextResponse.json({ message: "The requested basket is not available." }, { status: 403 });
  }

  const body = request.method === "GET" || request.method === "HEAD" ? undefined : await request.text();
  const upstreamPath = `/cart/${path.map(encodeURIComponent).join("/")}${new URL(request.url).search}`;

  try {
    const upstream = await fetch(gatewayUrl(upstreamPath), {
      method: request.method,
      headers: {
        Authorization: `Bearer ${session.accessToken}`,
        Accept: "application/json",
        ...(body ? { "Content-Type": request.headers.get("content-type") ?? "application/json" } : {}),
      },
      body,
      cache: "no-store",
    });
    return relay(upstream);
  } catch {
    return NextResponse.json({ message: "Basket is unavailable. Please try again." }, { status: 503 });
  }
}

async function relay(upstream: Response) {
  return new NextResponse(await upstream.text(), {
    status: upstream.status,
    headers: { "Content-Type": upstream.headers.get("content-type") ?? "application/json" },
  });
}
