import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title:
    "Intelligent Onboarding Command Center",
  description:
    "Operational monitoring dashboard for employee onboarding integrations, retries, health checks, and grounded AI guidance.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
