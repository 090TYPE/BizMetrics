import { defineConfig, devices } from "@playwright/test";
import path from "path";

/**
 * Run the full stack before e2e tests:
 *   docker compose up -d
 *   cd e2e && npm install && npx playwright install chromium && npm test
 *
 * Or point at a live instance:
 *   BASE_URL=https://bizmetrics-web.fly.dev npm test
 */
export default defineConfig({
  testDir: "./tests",
  fullyParallel: false, // tests share a running backend; run sequentially
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [["html", { open: "never" }], ["list"]],

  globalSetup: require.resolve("./global-setup"),

  use: {
    baseURL: process.env.BASE_URL ?? "http://localhost:5173",
    trace: "on-first-retry",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },

  projects: [
    {
      name: "chromium",
      use: {
        ...devices["Desktop Chrome"],
        // smoke tests use the pre-authenticated session by default;
        // auth tests override this by navigating to /login explicitly.
        storageState: path.join(__dirname, ".auth", "demo.json"),
      },
    },
  ],
});
