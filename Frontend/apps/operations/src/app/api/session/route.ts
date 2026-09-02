import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { gatewayUrl } from "@/lib/gateway/server";
import { hasSameOrigin } from "@/lib/http/same-origin";

const cookieName = "microshop_access_token";
export type CurrentUser = { userId: string; userName: string; role: string };

export async function GET() {
  const token = (await cookies()).get(cookieName)?.value;
  const user = token ? await getCurrentUser(token) : null;
  if (user) return NextResponse.json({ user });

  const response = NextResponse.json({ user: null }, { status: 401 });
  response.cookies.delete(cookieName);
  return response;
}

export async function POST(request: Request) {
  if (!hasSameOrigin(request)) return message("Cross-site requests are not accepted.", 403);
  const body = await json(request);
  if (!isCredentials(body)) return message("Username and password are required.", 400);

  try {
    const upstream = await fetch(gatewayUrl("/auth/login"), {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({ userName: body.userName.trim(), password: body.password }),
      cache: "no-store",
    });
    if (upstream.status === 401) return message("Username or password is incorrect.", 401);
    if (!upstream.ok) return message("Sign-in is unavailable. Try again shortly.", 503);

    const login = await json(upstream);
    if (!isLogin(login)) return message("Sign-in returned an invalid response.", 502);
    const user = await getCurrentUser(login.accessToken);
    if (!user) return message("Session could not be verified.", 502);
    if (user.role !== "Admin") return message("This workspace is restricted to administrators.", 403);

    const response = NextResponse.json({ user });
    response.cookies.set({ name: cookieName, value: login.accessToken, httpOnly: true, sameSite: "lax", secure: shouldUseSecureCookies(), path: "/", expires: new Date(login.expiresAt) });
    return response;
  } catch {
    return message("Sign-in is unavailable. Try again shortly.", 503);
  }
}

export async function DELETE(request: Request) {
  if (!hasSameOrigin(request)) return message("Cross-site requests are not accepted.", 403);

  const session = await getAdminSession(request);
  if (session) {
    try {
      const upstream = await fetch(gatewayUrl("/auth/logout"), {
        method: "POST",
        headers: { Authorization: `Bearer ${session.token}`, Accept: "application/json" },
        cache: "no-store",
      });
      if (!upstream.ok) return message("Sign-out could not be completed. Please try again.", 503);
    } catch {
      return message("Sign-out is temporarily unavailable. Please try again.", 503);
    }
  }

  const response = NextResponse.json({ success: true });
  response.cookies.set({ name: cookieName, value: "", httpOnly: true, sameSite: "lax", secure: shouldUseSecureCookies(), path: "/", expires: new Date(0) });
  return response;
}

export async function getAdminSession(request: Request): Promise<{ token: string; user: CurrentUser } | null> {
  const match = request.headers.get("cookie")?.match(new RegExp(`(?:^|;\\s*)${cookieName}=([^;]*)`));
  const token = match ? decodeURIComponent(match[1]) : null;
  if (!token) return null;
  const user = await getCurrentUser(token);
  return user?.role === "Admin" ? { token, user } : null;
}

async function getCurrentUser(token: string): Promise<CurrentUser | null> {
  try {
    const response = await fetch(gatewayUrl("/auth/me"), { headers: { Authorization: `Bearer ${token}`, Accept: "application/json" }, cache: "no-store" });
    const value = await json(response);
    return response.ok && isUser(value) ? value : null;
  } catch { return null; }
}
async function json(input: Request | Response): Promise<unknown> { try { return await input.json(); } catch { return null; } }
function isCredentials(value: unknown): value is { userName: string; password: string } { return isRecord(value) && typeof value.userName === "string" && typeof value.password === "string"; }
function isLogin(value: unknown): value is { accessToken: string; expiresAt: string } { return isRecord(value) && typeof value.accessToken === "string" && typeof value.expiresAt === "string"; }
function isUser(value: unknown): value is CurrentUser { return isRecord(value) && typeof value.userId === "string" && typeof value.userName === "string" && typeof value.role === "string"; }
function isRecord(value: unknown): value is Record<string, unknown> { return typeof value === "object" && value !== null; }
function shouldUseSecureCookies(): boolean {
  return process.env.MICROSHOP_COOKIE_SECURE !== "false" && process.env.NODE_ENV === "production";
}

function message(text: string, status: number) { return NextResponse.json({ message: text }, { status }); }