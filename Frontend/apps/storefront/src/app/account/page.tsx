import { AccountClient } from "./account-client";

export const metadata = {
  title: "Your account | MicroShop",
  description: "Manage your MicroShop account, delivery addresses, and orders.",
};

export default function AccountPage() {
  return <AccountClient />;
}