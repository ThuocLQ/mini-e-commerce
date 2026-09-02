import { ProcurementClient } from "./procurement-client";

export const metadata = {
  title: "Supplier & goods receipt | MicroShop Operations",
  description: "Manage supplier-backed purchase orders and server-confirmed goods receipts.",
};

export default function ProcurementPage() {
  return <ProcurementClient />;
}