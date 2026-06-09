import { expect, type Page } from "@playwright/test";

const API_URL = process.env.API_URL ?? "http://localhost:8080";

/** Seed demo data via the backend API. Safe to call multiple times. */
export async function seedDemo(request: ReturnType<Page["context"]>["request"] extends never ? never : any) {
  await request.post(`${API_URL}/api/demo/seed`);
}

/** Register a fresh throw-away account and return the credentials used. */
export async function registerFresh(page: Page): Promise<{ email: string; password: string }> {
  const email = `test+${Date.now()}@example.com`;
  const password = "TestPass1234!";

  await page.goto("/register");
  await page.getByLabel("Your name").fill("Test User");
  await page.getByLabel("Organization name").fill("Test Org");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Start free trial" }).click();

  // Should land on dashboard
  await expect(page).toHaveURL("/");
  return { email, password };
}

/** Log in with given credentials and wait for the dashboard. */
export async function loginAs(page: Page, email: string, password: string) {
  await page.goto("/login");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");
}

/** Log out via the UI. */
export async function logout(page: Page) {
  // TopBar has a "Sign out" button
  await page.getByRole("button", { name: /sign out/i }).click();
  await expect(page).toHaveURL("/login");
}
