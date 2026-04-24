const LEGACY_FAVORITES_KEY = "jobs:favorites";
const LEGACY_IMPORT_MARKER_PREFIX = "jobs:favorites-imported:";

type LegacyFavoriteEntry = {
  id?: string;
};

type LegacyFavoritesMap = Record<string, LegacyFavoriteEntry>;

function canUseStorage() {
  return typeof window !== "undefined";
}

function toImportMarkerKey(userId: string) {
  return `${LEGACY_IMPORT_MARKER_PREFIX}${userId}`;
}

function readLegacyFavoritesMap(): LegacyFavoritesMap {
  if (!canUseStorage()) {
    return {};
  }

  const raw = window.localStorage.getItem(LEGACY_FAVORITES_KEY);
  if (!raw) {
    return {};
  }

  try {
    const parsed = JSON.parse(raw) as unknown;
    return parsed && typeof parsed === "object" ? (parsed as LegacyFavoritesMap) : {};
  } catch {
    return {};
  }
}

function writeLegacyFavoritesMap(map: LegacyFavoritesMap) {
  if (!canUseStorage()) {
    return;
  }

  window.localStorage.setItem(LEGACY_FAVORITES_KEY, JSON.stringify(map));
}

export function listLegacyFavoriteIds(): string[] {
  const map = readLegacyFavoritesMap();
  const ids = new Set<string>();

  Object.entries(map).forEach(([key, value]) => {
    const id = typeof value?.id === "string" && value.id.trim() ? value.id : key;
    if (id && id.trim()) {
      ids.add(id.trim());
    }
  });

  return [...ids];
}

export function hasLegacyFavoritesToImport(): boolean {
  return listLegacyFavoriteIds().length > 0;
}

export function keepOnlyLegacyFavorites(idsToKeep: string[]) {
  const map = readLegacyFavoritesMap();
  const keep = new Set(idsToKeep);
  const next: LegacyFavoritesMap = {};

  Object.entries(map).forEach(([key, value]) => {
    const id = typeof value?.id === "string" && value.id.trim() ? value.id : key;
    if (!id || !keep.has(id)) {
      return;
    }

    next[key] = value;
  });

  if (Object.keys(next).length === 0) {
    clearLegacyFavorites();
    return;
  }

  writeLegacyFavoritesMap(next);
}

export function clearLegacyFavorites() {
  if (!canUseStorage()) {
    return;
  }

  window.localStorage.removeItem(LEGACY_FAVORITES_KEY);
}

export function markLegacyFavoritesImported(userId: string) {
  if (!canUseStorage() || !userId.trim()) {
    return;
  }

  window.localStorage.setItem(toImportMarkerKey(userId), "1");
}

export function wasLegacyFavoritesImported(userId: string): boolean {
  if (!canUseStorage() || !userId.trim()) {
    return false;
  }

  return window.localStorage.getItem(toImportMarkerKey(userId)) === "1";
}
