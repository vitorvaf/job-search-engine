"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useEffect, useMemo, useState } from "react";

type CsrfResponse = {
  csrfToken?: string;
};

export function SignInForm() {
  const searchParams = useSearchParams();

  const callbackUrl = useMemo(() => searchParams.get("callbackUrl") || "/", [searchParams]);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
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
    <section className="rounded-xl border border-line bg-white p-6 shadow-soft md:p-7">
      <h1 className="font-serif text-3xl text-ink">Entrar</h1>
      <p className="mt-2 text-sm text-muted">Use sua conta local ou login social para acessar seu painel.</p>

      <form className="mt-6 space-y-4" action="/api/auth/callback/credentials" method="post">
        <input type="hidden" name="csrfToken" value={csrfToken} />
        <input type="hidden" name="callbackUrl" value={callbackUrl} />
        <label className="block text-sm text-muted">
          Email
          <input
            type="email"
            name="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            required
            autoComplete="email"
            className="mt-1 w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink outline-none transition placeholder:text-muted/70 focus:border-ink"
          />
        </label>

        <label className="block text-sm text-muted">
          Senha
          <input
            type="password"
            name="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            required
            autoComplete="current-password"
            className="mt-1 w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink outline-none transition placeholder:text-muted/70 focus:border-ink"
          />
        </label>

        <button
          type="submit"
          className="w-full rounded-md border border-ink bg-ink px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent disabled:cursor-not-allowed disabled:opacity-60"
        >
          Entrar com email
        </button>
      </form>

      <div className="mt-5 grid gap-2">
        <a
          href={`/api/auth/signin/google?callbackUrl=${encodeURIComponent(callbackUrl)}`}
          className="rounded-md border border-line px-4 py-2 text-sm font-medium text-ink transition hover:border-ink disabled:cursor-not-allowed disabled:opacity-60"
        >
          Continuar com Google
        </a>
        <a
          href={`/api/auth/signin/github?callbackUrl=${encodeURIComponent(callbackUrl)}`}
          className="rounded-md border border-line px-4 py-2 text-sm font-medium text-ink transition hover:border-ink disabled:cursor-not-allowed disabled:opacity-60"
        >
          Continuar com GitHub
        </a>
      </div>

      <div className="mt-5 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
        <Link href="/esqueci-senha" className="underline-offset-2 hover:underline">
          Esqueci minha senha
        </Link>
        <Link href="/cadastro" className="underline-offset-2 hover:underline">
          Criar conta
        </Link>
      </div>
    </section>
  );
}
