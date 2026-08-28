"use client";

import { LoaderCircle, Mail } from "lucide-react";
import { useState } from "react";
import { problemMessage } from "@/lib/http/problem-details";

export function EmailVerificationStatus({ isVerified }: { isVerified: boolean }) {
  const [message, setMessage] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  if (isVerified) return null;

  async function resend() {
    setSending(true);
    setMessage(null);
    try {
      const response = await fetch("/api/email-verification/resend", { method: "POST", headers: { Accept: "application/json" } });
      const payload = await response.json().catch(() => null);
      setMessage(response.ok ? "Verification email sent." : messageOf(payload) ?? "Could not send verification email.");
    } catch {
      setMessage("Could not send verification email.");
    } finally {
      setSending(false);
    }
  }

  return <div className="hidden items-center gap-2 lg:flex">
    <span className="border border-[#d8d6c5] bg-[#fbfaf2] px-2 py-0.5 text-xs text-[#6f6317]">Email unverified</span>
    <button className="inline-flex h-8 items-center gap-1 px-2 text-xs font-medium text-[var(--accent)] hover:bg-[#e9f2ed] disabled:opacity-60" disabled={sending} onClick={resend} type="button">
      {sending ? <LoaderCircle className="animate-spin" size={14} /> : <Mail size={14} />}Resend
    </button>
    {message ? <span className="max-w-44 text-xs text-[var(--muted)]" role="status">{message}</span> : null}
  </div>;
}

const messageOf = problemMessage;
