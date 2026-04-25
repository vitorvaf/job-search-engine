"use client";

import { useEffect, useState } from "react";
import { Heart } from "lucide-react";
import { type Job } from "@/lib/types";
import { classNames } from "@/lib/utils";

type FavoriteButtonProps = {
  job: Job;
};

export function FavoriteButton({ job }: FavoriteButtonProps) {
  const [active, setActive] = useState(false);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function loadFavoriteState() {
      setIsLoading(true);

      try {
        const sessionResponse = await fetch("/api/auth/session", { cache: "no-store" });

        if (!sessionResponse.ok) {
          if (!cancelled) {
            setIsAuthenticated(false);
            setActive(false);
          }
          return;
        }

        const sessionPayload = (await sessionResponse.json()) as {
          user?: { id?: string | null };
        };

        const userId =
          typeof sessionPayload?.user?.id === "string" && sessionPayload.user.id.trim()
            ? sessionPayload.user.id.trim()
            : "";

        if (!userId) {
          if (!cancelled) {
            setIsAuthenticated(false);
            setActive(false);
          }
          return;
        }

        const favoritesResponse = await fetch("/api/favorites", { cache: "no-store" });
        if (!favoritesResponse.ok) {
          if (!cancelled) {
            setIsAuthenticated(favoritesResponse.status !== 401);
            setActive(false);
          }
          return;
        }

        const favoritesPayload = (await favoritesResponse.json()) as {
          items?: Array<{ id?: string }>;
        };

        const items = Array.isArray(favoritesPayload.items) ? favoritesPayload.items : [];
        const isFavorited = items.some((item) => item?.id === job.id);

        if (!cancelled) {
          setIsAuthenticated(true);
          setActive(isFavorited);
        }
      } catch {
        if (!cancelled) {
          setIsAuthenticated(false);
          setActive(false);
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    }

    void loadFavoriteState();

    return () => {
      cancelled = true;
    };
  }, [job.id]);

  const handleClick = async () => {
    if (isLoading || isSubmitting) {
      return;
    }

    if (!isAuthenticated) {
      const callback = `${window.location.pathname}${window.location.search}`;
      window.location.assign(`/entrar?callbackUrl=${encodeURIComponent(callback || "/")}`);
      return;
    }

    setIsSubmitting(true);

    try {
      const response = await fetch(`/api/favorites/${job.id}`, {
        method: active ? "DELETE" : "PUT",
        cache: "no-store",
      });

      if (response.status === 401) {
        const callback = `${window.location.pathname}${window.location.search}`;
        window.location.assign(`/entrar?callbackUrl=${encodeURIComponent(callback || "/")}`);
        return;
      }

      if (!response.ok) {
        return;
      }

      setActive((prev) => !prev);
    } finally {
      setIsSubmitting(false);
    }
  };

  const isDisabled = isLoading || isSubmitting;
  const label = !isAuthenticated ? "Entrar para favoritar" : active ? "Desfavoritar" : "Favoritar";

  return (
    <button
      type="button"
      onClick={handleClick}
      disabled={isDisabled}
      className={classNames(
        "inline-flex items-center gap-2 rounded-md border px-4 py-2 text-sm font-medium transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ink focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-70",
        active && isAuthenticated ? "border-ink bg-ink text-white" : "border-line bg-white text-ink hover:border-ink",
      )}
      aria-pressed={active}
    >
      <Heart className="h-4 w-4" aria-hidden="true" />
      {label}
    </button>
  );
}
