import { chromium, request as playwrightRequest, type FullConfig } from "@playwright/test";
import fs from "fs";
import path from "path";

/**
 * Runs once before all tests.
 * 1. Seeds demo workspace via API.
 * 2. Obtains JWT tokens via direct API call (no browser UI login — avoids
 *    consuming an auth rate-limit slot from the test suite's quota).
 * 3. Injects tokens into a browser page's localStorage and saves storageState
 *    to e2e/.auth/demo.json — smoke tests reuse it without re-authenticating.
 */
async function globalSetup(config: FullConfig) {
  const baseURL = config.projects[0].use.baseURL ?? "http://localhost:5173";
  const apiURL = process.env.API_URL ?? "http://localhost:8080";

  // Ensure .auth/ directory exists
  const authDir = path.join(__dirname, ".auth");
  if (!fs.existsSync(authDir)) fs.mkdirSync(authDir, { recursive: true });

  // ── 1. Seed demo data ────────────────────────────────────────────────────
  const apiCtx = await playwrightRequest.newContext({ baseURL: apiURL });
  await apiCtx.post("/api/demo/seed");

  // ── 2. Login via direct API call ─────────────────────────────────────────
  const loginRes = await apiCtx.post("/api/auth/login", {
    data: { email: "demo@bizmetrics.io", password: "demo1234" },
  });
  if (!loginRes.ok()) {
    const body = await loginRes.text();
    throw new Error(`Demo login failed (${loginRes.status()}): ${body}`);
  }
  const { accessToken, refreshToken } = (await loginRes.json()) as {
    accessToken: string;
    refreshToken: string;
  };
  await apiCtx.dispose();

  // ── 3. Inject tokens into browser localStorage and save storageState ─────
  const browser = await chromium.launch();
  const ctx = await browser.newContext();
  const page = await ctx.newPage();

  // Navigate to the app so we're on the right origin before touching localStorage
  await page.goto(`${baseURL}/login`);
  await page.evaluate(
    ({ at, rt }) => {
      localStorage.setItem("bm_access", at);
      localStorage.setItem("bm_refresh", rt);
    },
    { at: accessToken, rt: refreshToken },
  );

  await ctx.storageState({ path: path.join(authDir, "demo.json") });
  await browser.close();
}

export default globalSetup;
