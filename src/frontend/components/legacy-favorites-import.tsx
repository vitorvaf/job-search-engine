"use client";

import { useEffect, useState } from "react";
import {
  clearLegacyFavorites,
  hasLegacyFavoritesToImport,
  keepOnlyLegacyFavorites,
  listLegacyFavoriteIds,
  markLegacyFavoritesImported,
  wasLegacyFavoritesImported,
} from "@/lib/storage";

type LegacyFavoritesImportProps = {
  userId: string;
};

type ImportState = "hidden" | "ready" | "importing" | "done" | "partial";

export function LegacyFavoritesImport({ userId }: LegacyFavoritesImportProps) {
  const [state, setState] = useState<ImportState>("hidden");
  const [legacyIds, setLegacyIds] = useState<string[]>([]);
  const [importedCount, setImportedCount] = useState(0);

  useEffect(() => {
    if (!userId.trim()) {
      setState("hidden");
      return;
    }

    if (wasLegacyFavoritesImported(userId)) {
      setState("hidden");
      return;
    }

    if (!hasLegacyFavoritesToImport()) {
      setState("hidden");
      return;
    }

    const ids = listLegacyFavoriteIds();
    if (ids.length === 0) {
      setState("hidden");
      return;
    }

    setLegacyIds(ids);
    setState("ready");
  }, [userId]);

  const handleImport = async () => {
    if (state !== "ready") {
      return;
    }

    setState("importing");

    const results = await Promise.all(
      legacyIds.map(async (id) => {
        try {
          const response = await fetch(`/api/favorites/${id}`, {
            method: "PUT",
            cache: "no-store",
          });

          return { id, ok: response.ok };
        } catch {
          return { id, ok: false };
        }
      }),
    );

    const failedIds = results.filter((item) => !item.ok).map((item) => item.id);
    const imported = results.length - failedIds.length;

    setImportedCount(imported);

    if (failedIds.length === 0) {
      clearLegacyFavorites();
      markLegacyFavoritesImported(userId);
      setState("done");
    } else {
      keepOnlyLegacyFavorites(failedIds);
      setLegacyIds(failedIds);
      setState("partial");
    }

    window.dispatchEvent(new Event("favorites:updated"));
  };

  const handleDismiss = () => {
    markLegacyFavoritesImported(userId);
    setState("hidden");
  };

  if (state === "hidden") {
    return null;
  }

  return (
    <section className="rounded-xl border border-line bg-white p-4 shadow-soft">
      {state === "ready" ? (
        <>
          <h2 className="font-serif text-xl text-ink">Importar favoritos antigos?</h2>
          <p className="mt-2 text-sm text-muted">
            Encontramos {legacyIds.length} favorito(s) salvos localmente antes da migracao. Deseja importar para sua conta?
          </p>
          <div className="mt-4 flex flex-wrap gap-2">
            <button
              type="button"
              onClick={handleImport}
              className="rounded-md border border-ink bg-ink px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent"
            >
              Importar favoritos
            </button>
            <button
              type="button"
              onClick={handleDismiss}
              className="rounded-md border border-line px-4 py-2 text-sm font-medium text-ink transition hover:border-ink"
            >
              Agora nao
            </button>
          </div>
        </>
      ) : null}

      {state === "importing" ? <p className="text-sm text-muted">Importando favoritos antigos...</p> : null}

      {state === "done" ? (
        <p className="text-sm text-muted">Importacao concluida. {importedCount} favorito(s) adicionados a sua conta.</p>
      ) : null}

      {state === "partial" ? (
        <p className="text-sm text-muted">
          Importamos {importedCount} favorito(s), mas {legacyIds.length} ainda falharam. Voce pode tentar novamente.
        </p>
      ) : null}
    </section>
  );
}
