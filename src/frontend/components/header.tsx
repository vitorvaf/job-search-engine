"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { SignOutButton } from "@/components/auth/sign-out-button";

const links = [
  { href: "/", label: "Vagas" },
  { href: "/favoritos", label: "Favoritos" },
  { href: "/sobre", label: "Sobre" },
];

type SessionPayload = {
  user?: {
    id?: string;
    name?: string | null;
    email?: string | null;
  };
};

export function Header() {
  const [session, setSession] = useState<SessionPayload | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function loadSession() {
      try {
        const response = await fetch("/api/auth/session", { cache: "no-store" });
        if (!response.ok || cancelled) {
          return;
        }

        const payload = (await response.json()) as SessionPayload;
        if (!cancelled) {
          setSession(payload);
        }
      } catch {
        if (!cancelled) {
          setSession(null);
        }
      }
    }

    void loadSession();

    return () => {
      cancelled = true;
    };
  }, []);

  const isAuthenticated = Boolean(session?.user?.id);
  const userLabel =
    session?.user?.name?.trim() ||
    session?.user?.email?.split("@")[0]?.trim() ||
    "Minha conta";

  return (
    <header className="border-b border-line/80 bg-canvas">
      <div className="mx-auto flex max-w-3xl items-center justify-between px-4 py-6">
        <Link href="/" className="font-serif text-xl font-semibold tracking-tight text-ink">
          Jobs.
        </Link>
        <div className="flex items-center gap-4">
          <nav aria-label="Navegação principal" className="flex items-center gap-5">
            {links.map((link) => (
              <Link key={link.href} href={link.href} className="text-sm font-medium text-muted transition hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ink focus-visible:ring-offset-2">
                {link.label}
              </Link>
            ))}
          </nav>

          {isAuthenticated ? (
            <div className="flex items-center gap-2">
              <span className="rounded-md border border-line bg-white px-3 py-2 text-sm font-medium text-ink" title={session?.user?.email ?? undefined}>
                {userLabel}
              </span>
              <SignOutButton />
            </div>
          ) : (
            <Link
              href="/entrar"
              className="rounded-md border border-line px-3 py-2 text-sm font-medium text-ink transition hover:border-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ink focus-visible:ring-offset-2"
            >
              Entrar
            </Link>
          )}
        </div>
      </div>
    </header>
  );
}
