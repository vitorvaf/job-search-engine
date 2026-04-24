"use client";

import { useEffect, useState } from "react";

type CsrfResponse = {
  csrfToken?: string;
};

export function SignOutButton() {
  const [csrfToken, setCsrfToken] = useState("");

  useEffect(() => {
    let cancelled = false;

    async function loadToken() {
      const response = await fetch("/api/auth/csrf", { cache: "no-store" });
      if (!response.ok || cancelled) {
        return;
      }

      const payload = (await response.json()) as CsrfResponse;
      if (!cancelled && typeof payload.csrfToken === "string") {
        setCsrfToken(payload.csrfToken);
      }
    }

    void loadToken();

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <form action="/api/auth/signout" method="post" className="m-0">
      <input type="hidden" name="csrfToken" value={csrfToken} />
      <input type="hidden" name="callbackUrl" value="/" />
      <button
        type="submit"
        disabled={!csrfToken}
        className="rounded-md border border-line px-3 py-2 text-sm font-medium text-ink transition hover:border-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ink focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
      >
        Sair
      </button>
    </form>
  );
}
