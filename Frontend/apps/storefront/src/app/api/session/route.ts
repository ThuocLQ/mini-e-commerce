import { NextResponse } from "next/server";
import { cookies } from "next/headers";
import { gatewayUrl } from "@/lib/gateway/server";
import { hasSameOrigin } from "@/lib/http/same-origin";
import type { CurrentUser } from "@/lib/storefront/types";

const cookieName = "microshop_access_token";

type LoginResponse = {
  accessToken: string;
  expiresAt: string;
  tokenType: string;
};

export async function POST(request: Request) {
  if (!hasSameOrigin(request)) return message("Cross-site requests are not accepted.", 403);
  const body = await readJson(request);
  if (!isCredentials(body)) {
    return message("Username and password are required.", 400);
  }

  let loginResponse: Response;
  try {
    loginResponse = await fetch(gatewayUrl("/auth/login"), {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({ userName: body.userName.trim(), password: body.password }),
      cache: "no-store",
    });
  } catch {
    return message("Sign-in is unavailable. Please try again shortly.", 503);
  }

  if (loginResponse.status === 401) {
    return message("Username or password is incorrect.", 401);
  }
  if (!loginResponse.ok) {
    return message("Sign-in could not be completed. Please try again.", 502);
  }

  const login = await readJson(loginResponse);
  if (!isLogin(login)) {
    return message("Sign-in returned an invalid response.", 502);
  }

  const user = await currentUser(login.accessToken);
  if (!user) {
    return message("Sign-in completed but the session could not be verified.", 502);
  }

  const response = NextResponse.json({ user });
  response.cookies.set({
    name: cookieName,
    value: login.accessToken,
    httpOnly: true,
    sameSite: "lax",
    secure: shouldUseSecureCookies(),
    path: "/",
    expires: expiry(login.expiresAt),
  });
  return response;
}

export async function PUT(request: Request) {
  if (!hasSameOrigin(request)) return message("Cross-site requests are not accepted.", 403);

  const body = await readJson(request);
  if (!isCredentials(body)) {
    return message("Username and password are required.", 400);
  }

  try {
    const response = await fetch(gatewayUrl("/auth/register"), {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify({ userName: body.userName.trim(), password: body.password }),
      cache: "no-store",
    });

    if (response.status === 409) {
      return message("That username is already registered.", 409);
    }
    if (!response.ok) {
      return message("Account could not be created. Check the details and try again.", 400);
    }

    return NextResponse.json({ registered: true }, { status: 201 });
  } catch {
    return message("Account creation is unavailable. Please try again shortly.", 503);
  }
}

export async function GET() {
  const token = (await cookies()).get(cookieName)?.value;
  const user = token ? await currentUser(token) : null;

  if (user) {
    return NextResponse.json({ user });
  }

  const response = NextResponse.json({ user: null }, { status: 401 });
  response.cookies.delete(cookieName);
  return response;
}

export function DELETE() {
  const response = NextResponse.json({ success: true });
  response.cookies.set({
    name: cookieName,
    value: "",
    httpOnly: true,
    sameSite: "lax",
    secure: shouldUseSecureCookies(),
    path: "/",
    expires: new Date(0),
  });
  return response;
}

export async function getSessionFromRequest(
  request: Request,
): Promise<{ accessToken: string; user: CurrentUser } | null> {
  const token = readCookie(request.headers.get("cookie"));
  if (!token) return null;

  const user = await currentUser(token);
  return user ? { accessToken: token, user } : null;
}

async function currentUser(accessToken: string): Promise<CurrentUser | null> {
  try {
    const response = await fetch(gatewayUrl("/auth/me"), {
      headers: { Authorization: `Bearer ${accessToken}`, Accept: "application/json" },
      cache: "no-store",
    });
    if (!response.ok) return null;

    const payload = await readJson(response);
    return isCurrentUser(payload) ? payload : null;
  } catch {
    return null;
  }
}

async function readJson(input: Request | Response): Promise<unknown> {
  try {
    return await input.json();
  } catch {
    return null;
  }
}

function readCookie(header: string | null): string | null {
  const match = (header ?? "").match(new RegExp(`(?:^|;\\s*)${cookieName}=([^;]*)`));
  return match ? decodeURIComponent(match[1]) : null;
}

function expiry(value: string): Date {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) || parsed <= new Date()
    ? new Date(Date.now() + 15 * 60 * 1000)
    : parsed;
}

function isCredentials(value: unknown): value is { userName: string; password: string } {
  return isRecord(value)
    && typeof value.userName === "string"
    && value.userName.trim().length > 0
    && typeof value.password === "string"
    && value.password.length > 0;
}

function isLogin(value: unknown): value is LoginResponse {
  return isRecord(value)
    && typeof value.accessToken === "string"
    && value.accessToken.length > 0
    && typeof value.expiresAt === "string"
    && typeof value.tokenType === "string";
}

function isCurrentUser(value: unknown): value is CurrentUser {
  return isRecord(value)
    && typeof value.userId === "string"
    && typeof value.userName === "string"
    && typeof value.role === "string";
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function shouldUseSecureCookies(): boolean {
  return process.env.MICROSHOP_COOKIE_SECURE !== "false" && process.env.NODE_ENV === "production";
}

function message(text: string, status: number) {
  return NextResponse.json({ message: text }, { status });
}
