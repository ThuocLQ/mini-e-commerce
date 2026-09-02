import { defineConfig, devices } from "@playwright/test";

const storefrontUrl = process.env.STOREFRONT_E2E_BASE_URL ?? "http://127.0.0.1:3100";

export default defineConfig({
  testDir: "./tests",
  fullyParallel: false,
  workers: 1,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["github"], ["html", { open: "never" }]] : "list",
  use: {
    baseURL: storefrontUrl,
    trace: "retain-on-failure",
  },
  webServer: process.env.STOREFRONT_E2E_BASE_URL
    ? undefined
    : [
        {
          command: "node ./tests/mock-gateway.mjs",
          url: "http://127.0.0.1:4100/health",
          reuseExistingServer: !process.env.CI,
        },
        {
          command: "corepack pnpm --dir ../apps/storefront start",
          url: storefrontUrl,
          reuseExistingServer: !process.env.CI,
          env: {
            PORT: "3100",
            MICROSHOP_GATEWAY_BASE_URL: "http://127.0.0.1:4100",
            MICROSHOP_PUBLIC_ORIGIN: "http://127.0.0.1:3100",
            MICROSHOP_COOKIE_SECURE: "false",
          },
        },
      ],
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});