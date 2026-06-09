import { test, expect } from "@playwright/test";

/**
 * Smoke tests — run pre-authenticated as demo@bizmetrics.io.
 * The storageState (JWT in localStorage) is injected by the Playwright project
 * config via global-setup.ts, so no login calls are made here.
 */

const API_URL = process.env.API_URL ?? "http://localhost:8080";

// ── Navigation ───────────────────────────────────────────────────────────────

test("demo dashboard shows seeded datasets", async ({ page }) => {
  await page.goto("/");
  await expect(page).toHaveURL("/");
  await expect(page.getByText("Sales 2024")).toBeVisible();
  await expect(page.getByText("Marketing Q1-Q2 2024")).toBeVisible();
});

test("billing page shows plan and trial info", async ({ page }) => {
  await page.goto("/billing");
  await expect(page).toHaveURL("/billing");
  await expect(page.getByText(/pro|free|trialing/i).first()).toBeVisible();
});

test("team page shows demo member", async ({ page }) => {
  await page.goto("/team");
  await expect(page).toHaveURL("/team");
  await expect(page.getByText("demo@bizmetrics.io")).toBeVisible();
});

// ── Datasets ─────────────────────────────────────────────────────────────────

test("upload a CSV and see it in the list", async ({ page }) => {
  await page.goto("/");

  const csv = "Name,Score\nAlice,90\nBob,75\nCarol,88\n";
  const file = Buffer.from(csv, "utf-8");

  // Set file first (enables the upload button), then fill the optional name
  await page.locator('input[type="file"]').setInputFiles({
    name: "e2e-test.csv",
    mimeType: "text/csv",
    buffer: file,
  });
  await page.getByPlaceholder("Name (optional)").fill("E2E Test Data");
  await page.getByRole("button", { name: "Upload CSV" }).click();

  // May start as Pending, eventually shows in list
  await expect(page.getByText("E2E Test Data")).toBeVisible({ timeout: 10_000 });
});

// ── Dashboards ───────────────────────────────────────────────────────────────

test("dashboards page shows Sales Overview", async ({ page }) => {
  await page.goto("/dashboards");
  await expect(page).toHaveURL("/dashboards");
  await expect(page.getByText("Sales Overview")).toBeVisible();
});

test("dashboard view renders widget titles", async ({ page }) => {
  await page.goto("/dashboards");
  await page.getByText("Sales Overview").click();
  await expect(page.getByText("Revenue by Category")).toBeVisible({ timeout: 8_000 });
  await expect(page.getByText("Monthly Revenue Trend")).toBeVisible();
});

// ── API health ────────────────────────────────────────────────────────────────

test("API liveness probe returns ok", async ({ request }) => {
  const res = await request.get(`${API_URL}/health`);
  expect(res.ok()).toBeTruthy();
  const body = await res.json();
  expect(body.status).toBe("ok");
});

test("API readiness probe shows DB and storage healthy", async ({ request }) => {
  const res = await request.get(`${API_URL}/health/detail`);
  expect(res.ok()).toBeTruthy();
  const body = await res.json();
  expect(body.status).toBe("Healthy");
  expect(body.checks.database.status).toBe("Healthy");
  expect(body.checks.storage.status).toBe("Healthy");
});
