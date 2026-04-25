"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useEffect, useMemo, useState, type FormEvent } from "react";

type VerificationState = "idle" | "verifying" | "verified" | "failed";

export function VerifyEmailPanel() {
  const searchParams = useSearchParams();
  const token = useMemo(() => searchParams.get("token") || "", [searchParams]);
  const initialEmail = useMemo(() => searchParams.get("email") || "", [searchParams]);

  const [status, setStatus] = useState<VerificationState>(token ? "verifying" : "idle");
  const [email, setEmail] = useState(initialEmail);
  const [message, setMessage] = useState<string | null>(null);
  const [resending, setResending] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function verify() {
      if (!token) {
        setStatus("idle");
        return;
      }

      setStatus("verifying");
      setMessage(null);

      const response = await fetch("/api/account/verify-email", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ token }),
        cache: "no-store",
      });

      if (cancelled) {
        return;
      }

      if (response.ok) {
        setStatus("verified");
        return;
      }

      const payload = (await response.json().catch(() => null)) as { message?: string } | null;
      setStatus("failed");
      setMessage(payload?.message || "Não foi possível validar o token de verificação.");
    }

    void verify();

    return () => {
      cancelled = true;
    };
  }, [token]);

  const handleResend = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setMessage(null);

    if (!email.trim()) {
      setMessage("Informe o email para reenviar a verificação.");
      return;
    }

    setResending(true);

    try {
      const response = await fetch("/api/account/resend-verification", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: email.trim() }),
        cache: "no-store",
      });

      if (response.ok) {
        setMessage("Se a conta existir e ainda não estiver verificada, enviamos um novo email.");
        return;
      }

      const payload = (await response.json().catch(() => null)) as { message?: string } | null;
      setMessage(payload?.message || "Não foi possível reenviar o email agora.");
    } finally {
      setResending(false);
    }
  };

  return (
    <section className="rounded-xl border border-line bg-white p-6 shadow-soft md:p-7">
      <h1 className="font-serif text-3xl text-ink">Verificação de email</h1>

      {status === "verifying" ? <p className="mt-3 text-sm text-muted">Validando seu token de verificação...</p> : null}

      {status === "verified" ? (
        <div className="mt-4 space-y-3">
          <p className="text-sm text-ink">Email confirmado com sucesso. Agora você já pode entrar na sua conta.</p>
          <Link
            href="/entrar"
            className="inline-flex rounded-md border border-ink bg-ink px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent"
          >
            Ir para login
          </Link>
        </div>
      ) : null}

      {status === "idle" || status === "failed" ? (
        <div className="mt-4 space-y-4">
          <p className="text-sm text-muted">
            {status === "idle"
              ? "Enviamos um email com o link de verificação."
              : message || "Token inválido ou expirado. Solicite um novo email."}
          </p>

          <form className="space-y-3" onSubmit={handleResend}>
            <label className="block text-sm text-muted">
              Email
              <input
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                required
                autoComplete="email"
                className="mt-1 w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink outline-none transition placeholder:text-muted/70 focus:border-ink"
              />
            </label>

            <button
              type="submit"
              disabled={resending}
              className="w-full rounded-md border border-ink bg-ink px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent disabled:cursor-not-allowed disabled:opacity-60"
            >
              {resending ? "Reenviando..." : "Reenviar verificação"}
            </button>
          </form>

          {status !== "failed" && message ? <p className="text-sm text-muted">{message}</p> : null}
        </div>
      ) : null}
    </section>
  );
}
