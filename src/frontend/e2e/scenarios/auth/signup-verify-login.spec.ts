import { expect, test } from "@playwright/test";
import { waitForTokenFromMailpit } from "../../utils/mailpit";

test.describe("Auth Onboarding", () => {
  test("user can sign up, verify email, and login", async ({ page, baseURL }) => {
    test.setTimeout(120_000);

    if (!baseURL) {
      throw new Error("Playwright baseURL is not configured.");
    }

    const email = `e2e-ui-${crypto.randomUUID().slice(0, 8)}@example.com`;
    const password = "Password@123";
    const displayName = "QA Cadastro E2E";

    await page.goto("/cadastro", { waitUntil: "networkidle" });
    await page.getByLabel("Nome").fill(displayName);
    await page.getByLabel("Email").fill(email);
    await page.getByLabel(/^Senha$/).fill(password);
    await page.getByLabel("Confirmar senha").fill(password);
    await expect(page.getByRole("button", { name: "Criar conta" })).toBeEnabled();
    await page.getByRole("button", { name: "Criar conta" }).click();

    await expect(page).toHaveURL(new RegExp("/verificar-email"), { timeout: 30_000 });

    const verificationToken = await waitForTokenFromMailpit({
      mailpitBaseUrl: process.env.E2E_MAILPIT_URL ?? "http://localhost:8025",
      recipientEmail: email,
      subjectIncludes: "Confirme seu email",
    });

    await page.goto(`/verificar-email?token=${encodeURIComponent(verificationToken)}`, {
      waitUntil: "domcontentloaded",
    });

    await expect(page.getByText("Email confirmado com sucesso. Agora você já pode entrar na sua conta.")).toBeVisible();
    await page.getByRole("link", { name: "Ir para login" }).click();

    await expect(page).toHaveURL(new RegExp("/entrar"));
    await expect(page.locator('input[name="csrfToken"]')).toHaveValue(/.+/);
    await page.getByLabel("Email").fill(email);
    await page.getByLabel(/^Senha$/).fill(password);
    await page.getByRole("button", { name: "Entrar com email" }).click();

    await page.waitForURL((url) => !url.pathname.startsWith("/entrar"));
    await expect(page.getByText(displayName)).toBeVisible();
    await expect(page.getByRole("button", { name: "Sair" })).toBeVisible();
  });
});
