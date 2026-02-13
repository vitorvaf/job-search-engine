"use client";

import { useEffect, useState } from "react";
import { Heart } from "lucide-react";
import { type Job } from "@/lib/types";
import { isFavorite, toggleFavorite } from "@/lib/storage";
import { classNames } from "@/lib/utils";

type FavoriteButtonProps = {
  job: Job;
};

export function FavoriteButton({ job }: FavoriteButtonProps) {
  const [active, setActive] = useState(false);

  useEffect(() => {
    setActive(isFavorite(job.id));
  }, [job.id]);

  const handleClick = () => {
    const next = toggleFavorite(job);
    setActive(next);
  };

  return (
    <button
      type="button"
      onClick={handleClick}
      className={classNames(
        "inline-flex items-center gap-2 rounded-md border px-4 py-2 text-sm font-medium transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ink focus-visible:ring-offset-2",
        active ? "border-ink bg-ink text-white" : "border-line bg-white text-ink hover:border-ink",
      )}
      aria-pressed={active}
    >
      <Heart className="h-4 w-4" aria-hidden="true" />
      {active ? "Desfavoritar" : "Favoritar"}
    </button>
  );
}
