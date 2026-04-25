import { expect, test } from "@playwright/test";
import { createVerifiedLocalAccount } from "../../utils/account";

test.describe("Favorites Route Protection", () => {
  test("redirects guests to login and returns authenticated users to /favoritos", async ({ page, baseURL }) => {
    test.setTimeout(120_000);

    if (!baseURL) {
      throw new Error("Playwright baseURL is not configured.");
    }

    const account = await createVerifiedLocalAccount({
      appBaseUrl: baseURL,
      mailpitBaseUrl: process.env.E2E_MAILPIT_URL ?? "http://localhost:8025",
      displayName: "QA Favorites Guard",
    });

    await page.goto("/favoritos", { waitUntil: "domcontentloaded" });

    await expect(page).toHaveURL(/\/entrar\?callbackUrl=%2Ffavoritos/);

    const csrfResponse = await page.request.get("/api/auth/csrf");
    expect(csrfResponse.ok()).toBeTruthy();
    const csrfPayload = (await csrfResponse.json()) as { csrfToken?: string };
    expect(typeof csrfPayload.csrfToken).toBe("string");

    const loginResponse = await page.request.post("/api/auth/callback/credentials", {
      form: {
        csrfToken: csrfPayload.csrfToken ?? "",
        email: account.email,
        password: account.password,
        callbackUrl: "/",
      },
      maxRedirects: 0,
    });

    expect(loginResponse.status()).toBe(302);
    await page.goto("/favoritos", { waitUntil: "domcontentloaded" });

    await expect(page).toHaveURL(/\/favoritos/);
    await expect(page.getByRole("heading", { name: "Favoritos" })).toBeVisible();

    await page.reload({ waitUntil: "domcontentloaded" });

    await expect(page).toHaveURL(/\/favoritos/);
    await expect(page.getByRole("heading", { name: "Favoritos" })).toBeVisible();
  });
});
