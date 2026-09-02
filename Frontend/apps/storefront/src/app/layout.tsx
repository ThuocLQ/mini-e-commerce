import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "MicroShop | Everyday tools, thoughtfully chosen",
  description: "Discover current MicroShop products, then review and order with server-confirmed pricing and availability.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en" className="h-full antialiased">
      <body className="min-h-full flex flex-col">{children}</body>
    </html>
  );
}
