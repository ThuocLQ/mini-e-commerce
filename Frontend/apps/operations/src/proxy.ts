import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

const accessTokenCookie = "microshop_access_token";

export function proxy(request: NextRequest) {
  if (!request.cookies.has(accessTokenCookie)) {
    return NextResponse.redirect(new URL("/", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/inventory/:path*", "/orders/:path*", "/payments/:path*", "/procurement/:path*"],
};