"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { HeartOff } from "lucide-react";
import { EmptyState } from "@/components/empty-state";
import { type FavoriteJob } from "@/lib/types";
import { formatDate } from "@/lib/utils";
import { listFavorites, removeFavorite } from "@/lib/storage";

export function FavoritesList() {
  const [favorites, setFavorites] = useState<FavoriteJob[]>([]);

  useEffect(() => {
    setFavorites(listFavorites());
  }, []);

  const handleRemove = (id: string) => {
    removeFavorite(id);
    setFavorites(listFavorites());
  };

  if (favorites.length === 0) {
    return (
      <EmptyState
        title="Sem vagas favoritadas"
        description="Quando você favoritar uma vaga, ela aparece aqui para acesso rápido."
      />
    );
  }

  return (
    <section className="space-y-3">
      {favorites.map((job) => (
        <article key={job.id} className="rounded-xl border border-line bg-white p-5 shadow-soft">
          <div className="flex items-start justify-between gap-3">
            <div>
              <Link href={`/vagas/${job.id}`} className="font-serif text-2xl text-ink hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ink focus-visible:ring-offset-2">
                {job.title ?? "Título não informado"}
              </Link>
              <p className="mt-2 text-sm text-muted">
                {job.company ?? "Empresa não informada"} · {job.location ?? "Local não informado"}
              </p>
              <p className="mt-1 text-xs text-muted">{formatDate(job.postedAt)}</p>
            </div>

            <button
              type="button"
              onClick={() => handleRemove(job.id)}
              className="inline-flex items-center gap-2 rounded-md border border-line px-3 py-2 text-sm font-medium text-ink transition hover:border-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ink focus-visible:ring-offset-2"
            >
              <HeartOff className="h-4 w-4" aria-hidden="true" />
              Remover
            </button>
          </div>
        </article>
      ))}
    </section>
  );
}
