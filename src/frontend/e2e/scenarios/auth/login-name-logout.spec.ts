import { expect, test } from "@playwright/test";
import { createVerifiedLocalAccount } from "../../utils/account";

test.describe("Auth Header", () => {
  test("local login shows user name and logout returns to guest state", async ({ page, baseURL }) => {
    if (!baseURL) {
      throw new Error("Playwright baseURL is not configured.");
    }

    const account = await createVerifiedLocalAccount({
      appBaseUrl: baseURL,
      mailpitBaseUrl: process.env.E2E_MAILPIT_URL ?? "http://localhost:8025",
      displayName: "QA User E2E",
    });

    await page.goto("/entrar", { waitUntil: "networkidle" });

    await expect(page.locator('input[name="csrfToken"]')).toHaveValue(/.+/);
    await page.getByLabel("Email").fill(account.email);
    await page.getByLabel("Senha").fill(account.password);
    await page.getByRole("button", { name: "Entrar com email" }).click();

    await page.waitForURL((url) => !url.pathname.startsWith("/entrar"));

    await expect(page.getByText(account.displayName)).toBeVisible();
    await expect(page.getByRole("button", { name: "Sair" })).toBeVisible();
    await expect(page.locator('form[action="/api/auth/signout"] input[name="csrfToken"]')).toHaveValue(/.+/);

    await page.getByRole("button", { name: "Sair" }).click();

    await expect(page.getByRole("link", { name: "Entrar" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Sair" })).toHaveCount(0);
  });
});
