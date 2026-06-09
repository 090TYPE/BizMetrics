import { test, expect } from "@playwright/test";
import { registerFresh, loginAs, logout } from "./helpers";

test.describe("Authentication", () => {
  test("register a new account and land on dashboard", async ({ page }) => {
    const { email } = await registerFresh(page);
    await expect(page).toHaveURL("/");
    // Datasets section is visible on the dashboard
    await expect(page.getByText(/dataset/i).first()).toBeVisible();
    console.log(`Registered as ${email}`);
  });

  test("login redirects unauthenticated users", async ({ page }) => {
    // Clear any existing auth
    await page.goto("/");
    // Should redirect to /login
    await expect(page).toHaveURL(/login/);
  });

  test("login with valid credentials lands on dashboard", async ({ page }) => {
    const { email, password } = await registerFresh(page);
    await logout(page);
    await loginAs(page, email, password);
    await expect(page).toHaveURL("/");
  });

  test("login with wrong password shows error", async ({ page }) => {
    const { email } = await registerFresh(page);
    await logout(page);

    await page.goto("/login");
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Password").fill("wrongpassword");
    await page.getByRole("button", { name: "Sign in" }).click();

    await expect(page.locator(".error")).toBeVisible();
    await expect(page).toHaveURL("/login");
  });

  test("logout clears session and redirects to login", async ({ page }) => {
    await registerFresh(page);
    await logout(page);
    await expect(page).toHaveURL("/login");

    // Going to dashboard now redirects again
    await page.goto("/");
    await expect(page).toHaveURL(/login/);
  });

  test("register form validates required fields", async ({ page }) => {
    await page.goto("/register");
    await page.getByRole("button", { name: "Start free trial" }).click();
    // Browser validation prevents submission — still on /register
    await expect(page).toHaveURL("/register");
  });
});
