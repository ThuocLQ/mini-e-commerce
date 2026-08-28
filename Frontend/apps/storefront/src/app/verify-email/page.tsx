import { Suspense } from "react";
import { VerifyEmailClient } from "./verify-email-client";

export default function VerifyEmailPage() {
  return (
    <Suspense fallback={<main className="mx-auto flex min-h-screen w-full max-w-2xl items-center px-5 py-16"><p className="text-[var(--muted)]">Loading verification...</p></main>}>
      <VerifyEmailClient />
    </Suspense>
  );
}
