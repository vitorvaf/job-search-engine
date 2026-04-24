"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";

export function SignUpForm() {
  const router = useRouter();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);

    if (password.length < 8) {
      setError("A senha precisa ter pelo menos 8 caracteres.");
      return;
    }

    if (password !== confirmPassword) {
      setError("As senhas não conferem.");
      return;
    }

    setSubmitting(true);

    try {
      const response = await fetch("/api/account/register", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password, displayName: name }),
        cache: "no-store",
      });

      if (response.status === 409) {
        setError("Este email já está em uso.");
        return;
      }

      if (!response.ok) {
        const payload = (await response.json().catch(() => null)) as { message?: string } | null;
        setError(payload?.message || "Não foi possível criar a conta agora.");
        return;
      }

      router.push(`/verificar-email?email=${encodeURIComponent(email)}`);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <section className="rounded-xl border border-line bg-white p-6 shadow-soft md:p-7">
      <h1 className="font-serif text-3xl text-ink">Criar conta</h1>
      <p className="mt-2 text-sm text-muted">Cadastre-se para salvar vagas favoritas no seu painel.</p>

      <form className="mt-6 space-y-4" onSubmit={handleSubmit}>
        <label className="block text-sm text-muted">
          Nome
          <input
            type="text"
            value={name}
            onChange={(event) => setName(event.target.value)}
            autoComplete="name"
            className="mt-1 w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink outline-none transition placeholder:text-muted/70 focus:border-ink"
          />
        </label>

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

        <label className="block text-sm text-muted">
          Senha
          <input
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            required
            autoComplete="new-password"
            className="mt-1 w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink outline-none transition placeholder:text-muted/70 focus:border-ink"
          />
        </label>

        <label className="block text-sm text-muted">
          Confirmar senha
          <input
            type="password"
            value={confirmPassword}
            onChange={(event) => setConfirmPassword(event.target.value)}
            required
            autoComplete="new-password"
            className="mt-1 w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink outline-none transition placeholder:text-muted/70 focus:border-ink"
          />
        </label>

        {error ? <p className="text-sm text-red-700">{error}</p> : null}

        <button
          type="submit"
          disabled={submitting}
          className="w-full rounded-md border border-ink bg-ink px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent disabled:cursor-not-allowed disabled:opacity-60"
        >
          {submitting ? "Criando conta..." : "Criar conta"}
        </button>
      </form>

      <p className="mt-5 text-sm text-muted">
        Já tem conta?{" "}
        <Link href="/entrar" className="underline-offset-2 hover:underline">
          Entrar
        </Link>
      </p>
    </section>
  );
}
