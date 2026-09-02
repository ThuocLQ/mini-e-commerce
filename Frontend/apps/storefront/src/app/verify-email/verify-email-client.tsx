"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";

type State = "verifying" | "verified" | "invalid";

export function VerifyEmailClient() {
  const searchParams = useSearchParams();
  const [state, setState] = useState<State>("verifying");

  useEffect(() => {
    const task = window.setTimeout(() => {
      const token = searchParams.get("token");
      if (!token) { setState("invalid"); return; }

      void fetch("/api/email-verification", {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ token }),
      }).then(response => setState(response.ok ? "verified" : "invalid")).catch(() => setState("invalid"));
    }, 0);

    return () => window.clearTimeout(task);
  }, [searchParams]);

  const content = state === "verifying"
    ? ["Verifying your email", "Please wait while we confirm your address."]
    : state === "verified"
      ? ["Email verified", "Your account can now receive order updates."]
      : ["Verification link unavailable", "This link may have expired or was already used. Register again to receive a new link."];

  return (
    <main className="mx-auto flex min-h-screen w-full max-w-2xl items-center px-5 py-16">
      <section className="w-full border border-[var(--line)] bg-[var(--surface)] p-8 shadow-sm">
        <p className="text-sm font-medium text-[var(--accent)]">MicroShop account</p>
        <h1 className="mt-2 text-2xl font-semibold">{content[0]}</h1>
        <p className="mt-3 text-[var(--muted)]">{content[1]}</p>
        {state !== "verifying" ? <Link className="mt-7 inline-flex h-11 items-center bg-[var(--accent)] px-4 text-sm font-semibold text-white hover:bg-[var(--accent-strong)]" href="/">Return to store</Link> : null}
      </section>
    </main>
  );
}
