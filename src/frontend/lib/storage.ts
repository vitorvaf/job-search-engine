import { type FavoriteJob, type Job } from "@/lib/types";

const FAVORITES_KEY = "jobs:favorites";

type FavoriteMap = Record<string, FavoriteJob>;

function canUseStorage() {
  return typeof window !== "undefined";
}

function readFavoritesMap(): FavoriteMap {
  if (!canUseStorage()) return {};

  const raw = window.localStorage.getItem(FAVORITES_KEY);
  if (!raw) return {};

  try {
    const parsed = JSON.parse(raw) as FavoriteMap;
    return parsed && typeof parsed === "object" ? parsed : {};
  } catch {
    return {};
  }
}

function writeFavoritesMap(map: FavoriteMap) {
  if (!canUseStorage()) return;
  window.localStorage.setItem(FAVORITES_KEY, JSON.stringify(map));
}

export function toFavoriteSnapshot(job: Job): FavoriteJob {
  return {
    id: job.id,
    title: job.title,
    company: job.company,
    location: job.location,
    postedAt: job.postedAt,
    sourceName: job.sourceName,
  };
}

export function listFavorites(): FavoriteJob[] {
  return Object.values(readFavoritesMap());
}

export function isFavorite(id: string): boolean {
  return Boolean(readFavoritesMap()[id]);
}

export function toggleFavorite(job: Job): boolean {
  const map = readFavoritesMap();

  if (map[job.id]) {
    delete map[job.id];
    writeFavoritesMap(map);
    return false;
  }

  map[job.id] = toFavoriteSnapshot(job);
  writeFavoritesMap(map);
  return true;
}

export function removeFavorite(id: string) {
  const map = readFavoritesMap();
  if (!map[id]) return;
  delete map[id];
  writeFavoritesMap(map);
}
