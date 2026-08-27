"use client";

import Link from "next/link";
import { ArrowLeft, CircleAlert, FileWarning, PackageCheck, ShieldAlert, Truck } from "lucide-react";
import { OperationsWorkspace } from "@/components/operations-workspace";

export default function ProcurementPage() {
  return <OperationsWorkspace area="procurement"><main className="orders-page">
    <header className="orders-header">
      <Link className="back" href="/"><ArrowLeft size={17} />Catalog control</Link>
      <div className="orders-title"><div><p className="eyebrow">Operations / blocked workstream</p><h1>Supplier & goods receipt</h1><p className="page-summary">This workspace deliberately does not create supplier records, purchase orders, or stock receipts until the Gateway exposes a verified operations contract.</p></div><FileWarning aria-hidden="true" size={28} /></div>
    </header>

    <div className="notice procurement-blocker" role="alert"><CircleAlert size={19} /><span>No supplier or procurement endpoint is implemented in the source currently routed by the API Gateway. A Gateway route alone is not a safe contract for an operational action.</span></div>

    <section className="operations-section blocker-grid" aria-labelledby="blocker-heading">
      <div className="section-heading"><div><p className="eyebrow">Backend handoff required</p><h2 id="blocker-heading">What this workstream needs</h2></div><p>These fields and state transitions must be server-owned and confirmed before controls appear here.</p></div>
      <div className="table-wrap"><table><thead><tr><th>Operator task</th><th>Gateway capability required</th><th>Why it matters</th></tr></thead><tbody>
        <tr><td><strong><Truck size={16} />Maintain suppliers</strong></td><td><code>GET/POST /suppliers</code> with role enforcement, stable supplier ID, active state, and validation errors.</td><td>Supplier identity and contact data must be returned by the server, not inferred from the catalog.</td></tr>
        <tr><td><strong><PackageCheck size={16} />Create and submit a purchase order</strong></td><td>Versioned purchase-order list/detail plus create and submit transitions, line pricing/currency, idempotency, and audit fields.</td><td>A browser cannot safely assume draft, submitted, or cancellation rules.</td></tr>
        <tr><td><strong><ShieldAlert size={16} />Confirm a goods receipt</strong></td><td>Server-confirmed receipt endpoint with receipt ID, source purchase order ID, accepted/rejected/pending result, duplicate behavior, and inventory refresh signal.</td><td>Receipt confirmation changes on-hand stock and must be exactly-once from the operator’s perspective.</td></tr>
      </tbody></table></div>
    </section>

    <section className="operations-section"><div className="section-heading"><div><p className="eyebrow">Available now</p><h2>Safe next steps</h2></div></div><div className="empty"><p>Use Inventory reconciliation to identify quantity exceptions. It remains read-only until a receipt contract is available.</p><Link className="command" href="/inventory">Open inventory reconciliation</Link></div></section>
  </main></OperationsWorkspace>;
}
