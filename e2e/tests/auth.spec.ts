import { test, expect } from "@playwright/test";

/**
 * Auth tests — each test clears storage to start as a logged-out user,
 * overriding the pre-authenticated storageState set at the project level.
 */

test.beforeEach(async ({ page }) => {
  // Start every auth test with a clean slate (no JWT in localStorage)
  await page.context().clearCookies();
  await page.goto("/");
  await page.evaluate(() => localStorage.clear());
});

async function registerFresh(page: Parameters<typeof test>[1] extends never ? never : any) {
  const email = `test+${Date.now()}@example.com`;
  const password = "TestPass1234!";

  await page.goto("/register");
  await page.getByLabel("Your name").fill("Test User");
  await page.getByLabel("Organization name").fill("Test Org");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Start free trial" }).click();
  await expect(page).toHaveURL("/", { timeout: 8_000 });
  return { email, password };
}

// ── Tests ─────────────────────────────────────────────────────────────────────

test("register a new account lands on dashboard", async ({ page }) => {
  const { email } = await registerFresh(page);
  await expect(page).toHaveURL("/");
  await expect(page.getByText(/dataset/i).first()).toBeVisible();
  console.log(`Registered: ${email}`);
});

test("unauthenticated visit to / redirects to login", async ({ page }) => {
  await page.goto("/");
  await expect(page).toHaveURL(/login/);
});

test("login with valid credentials lands on dashboard", async ({ page }) => {
  const { email, password } = await registerFresh(page);
  // Log out
  await page.goto("/");
  await page.evaluate(() => localStorage.clear());
  // Log back in
  await page.goto("/login");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/", { timeout: 8_000 });
});

test("login with wrong password shows error message", async ({ page }) => {
  const { email } = await registerFresh(page);
  await page.evaluate(() => localStorage.clear());

  await page.goto("/login");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill("wrongpassword");
  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(page.locator(".error")).toBeVisible();
  await expect(page).toHaveURL("/login");
});

test("clearing localStorage logs the user out", async ({ page }) => {
  await registerFresh(page);
  await expect(page).toHaveURL("/");

  // Simulate logout by clearing storage and reloading
  await page.evaluate(() => localStorage.clear());
  await page.goto("/");
  await expect(page).toHaveURL(/login/);
});

test("register form requires all fields", async ({ page }) => {
  await page.goto("/register");
  await page.getByRole("button", { name: "Start free trial" }).click();
  // Browser HTML5 validation keeps the user on /register
  await expect(page).toHaveURL("/register");
});
