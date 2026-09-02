"use client";

import { LoaderCircle, X } from "lucide-react";
import { FormEvent, useRef, useState } from "react";
import { useDialogFocus } from "@/hooks/use-dialog-focus";
import type { CurrentUser } from "@/lib/storefront/types";

type Feedback = { tone: "error" | "success"; text: string };

type AuthDialogProps = {
  open: boolean;
  notice: string | null;
  onClose: () => void;
  onSignedIn: (user: CurrentUser) => void;
};

export function AuthDialog({ open, notice, onClose, onSignedIn }: AuthDialogProps) {
  const [userName, setUserName] = useState("");
  const [password, setPassword] = useState("");
  const [email, setEmail] = useState("");
  const [feedback, setFeedback] = useState<Feedback | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [mode, setMode] = useState<"sign-in" | "register">("sign-in");

  const isRegistering = mode === "register";

  const dialogRef = useRef<HTMLElement>(null);
  useDialogFocus({ dialogRef, isOpen: open, onClose });

  if (!open) return null;

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setFeedback(null);

    const isRegistering = mode === "register";

    try {
      const response = await fetch("/api/session", {
        method: isRegistering ? "PUT" : "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify({ userName, email, password }),
      });
      const payload: unknown = await response.json().catch(() => null);

      if (isRegistering && response.ok && isRegistrationPayload(payload)) {
        setMode("sign-in");
        setFeedback({ tone: "success", text: "Account created. Check your inbox to verify your email before receiving order updates." });
        return;
      }

      if (!response.ok || !isSessionPayload(payload)) {
        setFeedback({ tone: "error", text: getMessage(payload) ?? (isRegistering ? "Account could not be created." : "Sign-in could not be completed.") });
        return;
      }

      onSignedIn(payload.user);
      setPassword("");
    } catch {
      setFeedback({ tone: "error", text: isRegistering ? "Account creation is unavailable. Please try again shortly." : "Sign-in is unavailable. Please try again shortly." });
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 grid overflow-y-auto bg-black/35 px-4 py-4 sm:place-items-center sm:py-8" role="presentation">
      <section aria-labelledby="account-title" aria-modal="true" className="my-auto w-full max-w-md border border-[var(--line)] bg-[var(--surface)] p-6 shadow-xl" ref={dialogRef} role="dialog" tabIndex={-1}>
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="text-sm font-medium text-[var(--accent)]">Account</p>
            <h2 className="mt-1 text-xl font-semibold" id="account-title">{isRegistering ? "Create your account" : "Sign in to continue"}</h2>
          </div>
          <button aria-label="Close account dialog" className="grid size-9 place-items-center border border-[var(--line)] text-[var(--muted)] hover:bg-[#f3f5f2]" onClick={onClose} type="button">
            <X aria-hidden="true" size={18} />
          </button>
        </div>

        <form className="mt-6 space-y-4" onSubmit={submit}>
          {notice ? <p className="border-l-2 border-[#d8d6c5] bg-[#fbfaf2] px-3 py-2 text-sm text-[#6f6317]" role="status">{notice}</p> : null}
          <label className="block text-sm font-medium">Username<input autoComplete="username" data-dialog-initial-focus="true" className="mt-2 h-11 w-full border border-[var(--line)] bg-white px-3 text-base outline-none focus:border-[var(--accent)]" onChange={(event) => setUserName(event.target.value)} required value={userName} /></label>
          {isRegistering ? <label className="block text-sm font-medium">Email<input autoComplete="email" className="mt-2 h-11 w-full border border-[var(--line)] bg-white px-3 text-base outline-none focus:border-[var(--accent)]" onChange={(event) => setEmail(event.target.value)} required type="email" value={email} /></label> : null}
          <label className="block text-sm font-medium">Password{isRegistering ? <span className="ml-2 text-xs font-normal text-[var(--muted)]">At least 14 characters</span> : null}<input autoComplete={isRegistering ? "new-password" : "current-password"} className="mt-2 h-11 w-full border border-[var(--line)] bg-white px-3 text-base outline-none focus:border-[var(--accent)]" minLength={isRegistering ? 14 : 1} onChange={(event) => setPassword(event.target.value)} required type="password" value={password} /></label>
          {feedback ? <p aria-live="polite" className={feedback.tone === "success" ? "border-l-2 border-[var(--accent)] bg-[#f4fbf6] px-3 py-2 text-sm text-[var(--accent-strong)]" : "border-l-2 border-[var(--danger)] bg-[#fff7f6] px-3 py-2 text-sm text-[var(--danger)]"} role={feedback.tone === "success" ? "status" : "alert"}>{feedback.text}</p> : null}
          <button className="inline-flex h-11 w-full items-center justify-center gap-2 bg-[var(--accent)] px-4 text-sm font-semibold text-white hover:bg-[var(--accent-strong)] disabled:cursor-not-allowed disabled:bg-[#8ba89b]" disabled={isSubmitting} type="submit">{isSubmitting ? <LoaderCircle aria-hidden="true" className="animate-spin" size={17} /> : null}{isSubmitting ? (isRegistering ? "Creating account" : "Signing in") : (isRegistering ? "Create account" : "Sign in")}</button>
          <button className="w-full text-sm font-medium text-[var(--accent)] hover:underline" disabled={isSubmitting} onClick={() => { setMode(isRegistering ? "sign-in" : "register"); setFeedback(null); }} type="button">{isRegistering ? "Use an existing account" : "Create an account"}</button>
        </form>
      </section>
    </div>
  );
}

function isSessionPayload(value: unknown): value is { user: CurrentUser } {
  if (typeof value !== "object" || value === null) return false;
  const user = (value as { user?: unknown }).user;
  return typeof user === "object" && user !== null
    && typeof (user as Record<string, unknown>).userId === "string"
    && typeof (user as Record<string, unknown>).userName === "string"
    && typeof (user as Record<string, unknown>).role === "string";
}

function isRegistrationPayload(value: unknown): value is { registered: true } {
  return typeof value === "object" && value !== null && (value as Record<string, unknown>).registered === true;
}

function getMessage(value: unknown): string | null {
  return typeof value === "object" && value !== null && typeof (value as Record<string, unknown>).message === "string"
    ? (value as Record<string, string>).message
    : null;
}
