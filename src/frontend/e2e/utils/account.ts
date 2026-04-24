import { waitForTokenFromMailpit } from "./mailpit";

export type E2EAccount = {
  email: string;
  password: string;
  displayName: string;
};

export async function createVerifiedLocalAccount(options: {
  appBaseUrl: string;
  mailpitBaseUrl: string;
  password?: string;
  displayName?: string;
}): Promise<E2EAccount> {
  const appBaseUrl = options.appBaseUrl.replace(/\/$/, "");
  const mailpitBaseUrl = options.mailpitBaseUrl.replace(/\/$/, "");
  const password = options.password ?? "Password@123";
  const displayName = options.displayName ?? "QA User E2E";
  const email = `e2e-${crypto.randomUUID().slice(0, 8)}@example.com`;

  const registerResponse = await fetch(`${appBaseUrl}/api/account/register`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ email, password, displayName }),
  });

  if (registerResponse.status !== 201 && registerResponse.status !== 200) {
    const raw = await registerResponse.text();
    throw new Error(`Failed to register E2E account. status=${registerResponse.status} body=${raw}`);
  }

  const verificationToken = await waitForTokenFromMailpit({
    mailpitBaseUrl,
    recipientEmail: email,
    subjectIncludes: "Confirme seu email",
  });

  const verifyResponse = await fetch(`${appBaseUrl}/api/account/verify-email`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ token: verificationToken }),
  });

  if (!verifyResponse.ok) {
    const raw = await verifyResponse.text();
    throw new Error(`Failed to verify E2E account. status=${verifyResponse.status} body=${raw}`);
  }

  return { email, password, displayName };
}
