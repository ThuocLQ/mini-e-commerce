"use client";

import Link from "next/link";
import { Boxes, CircleAlert, ClipboardList, CreditCard, LoaderCircle, LogOut, PackageSearch, ShieldCheck, Truck } from "lucide-react";
import { useEffect, useState, type ReactNode } from "react";

type Area = "catalog" | "inventory" | "orders" | "payments" | "procurement";
type User = { userId: string; userName: string; role: string };

const navigation: Array<{ area: Area; href: string; label: string; Icon: typeof Boxes }> = [
  { area: "catalog", href: "/", label: "Catalog & prices", Icon: Boxes },
  { area: "inventory", href: "/inventory", label: "Stock health", Icon: PackageSearch },
  { area: "orders", href: "/orders", label: "Order & payment queue", Icon: ClipboardList },
  { area: "payments", href: "/payments", label: "Payment ledger", Icon: CreditCard },
  { area: "procurement", href: "/procurement", label: "Supply & receipt", Icon: Truck },
];

export function OperationsWorkspace({ area, children }: { area: Area; children: ReactNode }) {
  const [user, setUser] = useState<User | null | undefined>(undefined);
  const [sessionError, setSessionError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    async function checkSession() {
      try {
        setSessionError(null);
        const response = await fetch("/api/session");
        const payload = response.ok ? await response.json().catch(() => null) : null;
        const currentUser = isUser(payload?.user) && payload.user.role === "Admin" ? payload.user : null;
        if (!active) return;
        setUser(currentUser);
        if (!currentUser) window.location.replace("/");
      } catch {
        if (active) { setUser(null); setSessionError("Administrator access could not be checked. Return to sign in and try again."); }
      }
    }
    void checkSession();
    return () => { active = false; };
  }, []);

  if (user === undefined) return <main className="signin"><div><LoaderCircle className="spin" size={22} /><p>Checking administrator access…</p></div></main>;
  if (!user) return <main className="signin"><div>{sessionError ? <><CircleAlert size={22} /><p className="inline-error">{sessionError}</p><button className="command" onClick={() => window.location.replace("/")}>Return to sign in</button></> : <><LoaderCircle className="spin" size={22} /><p>Returning to sign in…</p></>}</div></main>;

  return <main className="shell workspace-shell">
    <aside className="sidebar">
      <div className="brand"><span className="brand-mark"><Boxes size={19} /></span><span>MicroShop</span></div>
      <nav className="workspace-nav" aria-label="Operations workspace">
        {navigation.map(({ area: itemArea, href, label, Icon }) => <Link className={itemArea === area ? "nav-active" : ""} href={href} key={itemArea}><Icon size={17} />{label}</Link>)}
      </nav>
      <div className="operator"><ShieldCheck size={17} /><span>{user.userName}</span><button aria-label="Sign out" title="Sign out" onClick={async () => { await fetch("/api/session", { method: "DELETE" }); window.location.replace("/"); }}><LogOut size={17} /></button></div>
    </aside>
    <section className="workspace">{children}</section>
  </main>;
}

function isUser(value: unknown): value is User {
  return typeof value === "object" && value !== null
    && typeof (value as Record<string, unknown>).userId === "string"
    && typeof (value as Record<string, unknown>).userName === "string"
    && typeof (value as Record<string, unknown>).role === "string";
}
