"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";

export function ForgotPasswordForm() {
  const [email, setEmail] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitting(true);
    setMessage(null);

    try {
      await fetch("/api/account/password/forgot", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: email.trim() }),
        cache: "no-store",
      });

      setMessage("Se a conta existir, enviamos instruções para redefinir a senha.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <section className="rounded-xl border border-line bg-white p-6 shadow-soft md:p-7">
      <h1 className="font-serif text-3xl text-ink">Recuperar senha</h1>
      <p className="mt-2 text-sm text-muted">Informe seu email para receber o link de redefinição.</p>

      <form className="mt-6 space-y-4" onSubmit={handleSubmit}>
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
          disabled={submitting}
          className="w-full rounded-md border border-ink bg-ink px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent disabled:cursor-not-allowed disabled:opacity-60"
        >
          {submitting ? "Enviando..." : "Enviar link de recuperação"}
        </button>
      </form>

      {message ? <p className="mt-4 text-sm text-muted">{message}</p> : null}

      <p className="mt-5 text-sm text-muted">
        <Link href="/entrar" className="underline-offset-2 hover:underline">
          Voltar para login
        </Link>
      </p>
    </section>
  );
}
