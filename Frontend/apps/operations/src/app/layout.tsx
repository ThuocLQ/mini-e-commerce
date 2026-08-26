import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = { title: "MicroShop Operations", description: "Operational workspace" };
export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) { return <html lang="en"><body>{children}</body></html>; }