import { test, expect, request as playwrightRequest } from "@playwright/test";
import { registerFresh, loginAs, logout } from "./helpers";

const API_URL = process.env.API_URL ?? "http://localhost:8080";

test.describe("App smoke tests", () => {
  test.beforeAll(async () => {
    // Seed demo data once before the suite
    const ctx = await playwrightRequest.newContext();
    await ctx.post(`${API_URL}/api/demo/seed`);
    await ctx.dispose();
  });

  // ── Navigation ─────────────────────────────────────────────────────────────

  test("demo user can log in and see dashboard", async ({ page }) => {
    await loginAs(page, "demo@bizmetrics.io", "demo1234");
    await expect(page).toHaveURL("/");
    await expect(page.getByText("Sales 2024")).toBeVisible();
  });

  test("billing page shows plan info", async ({ page }) => {
    await loginAs(page, "demo@bizmetrics.io", "demo1234");
    await page.getByRole("link", { name: /billing/i }).click();
    await expect(page).toHaveURL("/billing");
    await expect(page.getByText(/pro|free|trialing/i).first()).toBeVisible();
  });

  test("team page loads member list", async ({ page }) => {
    await loginAs(page, "demo@bizmetrics.io", "demo1234");
    await page.getByRole("link", { name: /team/i }).click();
    await expect(page).toHaveURL("/team");
    await expect(page.getByText("demo@bizmetrics.io")).toBeVisible();
  });

  // ── Datasets ───────────────────────────────────────────────────────────────

  test("datasets are listed on dashboard", async ({ page }) => {
    await loginAs(page, "demo@bizmetrics.io", "demo1234");
    await expect(page.getByText("Sales 2024")).toBeVisible();
    await expect(page.getByText("Marketing Q1-Q2 2024")).toBeVisible();
  });

  test("upload a CSV and see it appear in the list", async ({ page }) => {
    const { email, password } = await registerFresh(page);

    // Create a minimal CSV in memory
    const csv = "Name,Score\nAlice,90\nBob,75\nCarol,88\n";
    const file = Buffer.from(csv, "utf-8");

    // Find upload input and fill name
    await page.getByPlaceholder(/dataset name/i).fill("My Test Data");
    await page.locator('input[type="file"]').setInputFiles({
      name: "test.csv",
      mimeType: "text/csv",
      buffer: file,
    });
    await page.getByRole("button", { name: /upload/i }).click();

    // Dataset should appear in the list (may be Pending/Processing/Ready)
    await expect(page.getByText("My Test Data")).toBeVisible({ timeout: 10_000 });

    await logout(page);
  });

  // ── Dashboards ─────────────────────────────────────────────────────────────

  test("dashboards page shows saved dashboard", async ({ page }) => {
    await loginAs(page, "demo@bizmetrics.io", "demo1234");
    await page.getByRole("link", { name: /dashboards/i }).click();
    await expect(page).toHaveURL("/dashboards");
    await expect(page.getByText("Sales Overview")).toBeVisible();
  });

  test("dashboard view renders widgets", async ({ page }) => {
    await loginAs(page, "demo@bizmetrics.io", "demo1234");
    await page.getByRole("link", { name: /dashboards/i }).click();
    await page.getByText("Sales Overview").click();
    // Widget titles should appear
    await expect(page.getByText("Revenue by Category")).toBeVisible({ timeout: 8_000 });
    await expect(page.getByText("Monthly Revenue Trend")).toBeVisible();
  });

  // ── Health endpoints ───────────────────────────────────────────────────────

  test("API liveness probe returns ok", async ({ request }) => {
    const res = await request.get(`${API_URL}/health`);
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(body.status).toBe("ok");
  });
});
