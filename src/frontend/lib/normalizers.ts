import { DEFAULT_PAGE_SIZE } from "@/lib/constants";
import {
  type ApiFavoritesResponse,
  type ApiJobDetail,
  type ApiJobListItem,
  type ApiJobsResponse,
  type ApiSource,
  type FavoriteJobsResponse,
  type Job,
  type JobsResponse,
  type Source,
} from "@/lib/types";

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null ? (value as Record<string, unknown>) : {};
}

function safeString(value: unknown): string | undefined {
  return typeof value === "string" && value.trim() ? value : undefined;
}

function safeArray(value: unknown): string[] | undefined {
  if (Array.isArray(value)) {
    const out = value.filter((item): item is string => typeof item === "string" && item.trim().length > 0);
    return out.length > 0 ? out : undefined;
  }

  if (typeof value === "string" && value.trim()) {
    const out = value
      .split(",")
      .map((item) => item.trim())
      .filter(Boolean);
    return out.length > 0 ? out : undefined;
  }

  return undefined;
}

function safeNumber(value: unknown, fallback: number): number {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

export function normalizeJob(raw: ApiJobListItem | ApiJobDetail | unknown): Job {
  const item = asRecord(raw);
  const companyObj = asRecord(item.company);
  const sourceObj = asRecord(item.source);
  const locationObj = asRecord(item.location);

  return {
    id: safeString(item.id) ?? "",
    title: safeString(item.title),
    company: safeString(item.company) ?? safeString(companyObj.name),
    location:
      safeString(item.locationText) ??
      safeString(item.location) ??
      safeString(locationObj.city) ??
      safeString(locationObj.state),
    workMode: safeString(item.workMode),
    seniority: safeString(item.seniority),
    employmentType: safeString(item.employmentType),
    tags: safeArray(item.tags),
    description: safeString(item.description) ?? safeString(item.descriptionText),
    applyUrl: safeString(item.applyUrl) ?? safeString(sourceObj.url),
    sourceName: safeString(item.sourceName) ?? safeString(sourceObj.name),
    postedAt: safeString(item.postedAt),
  };
}

export function normalizeJobsResponse(raw: ApiJobsResponse | unknown, page = 1, pageSize = DEFAULT_PAGE_SIZE): JobsResponse {
  const data = asRecord(raw);

  let itemsRaw: unknown = data.items;
  let currentPage = safeNumber(data.page, page);
  let currentPageSize = safeNumber(data.pageSize, pageSize);
  let total = typeof data.total === "number" ? data.total : undefined;
  let totalPages = typeof data.totalPages === "number" ? data.totalPages : undefined;

  if (!Array.isArray(itemsRaw) && Array.isArray(data.data)) {
    itemsRaw = data.data;
    const meta = asRecord(data.meta);
    currentPage = safeNumber(meta.page, page);
    currentPageSize = safeNumber(meta.pageSize, pageSize);
    total = typeof meta.total === "number" ? meta.total : total;
    totalPages = typeof meta.totalPages === "number" ? meta.totalPages : totalPages;
  }

  const items = Array.isArray(itemsRaw) ? itemsRaw.map((item) => normalizeJob(item)).filter((item) => item.id) : [];

  return {
    items,
    page: currentPage,
    pageSize: currentPageSize,
    total,
    totalPages,
  };
}

export function normalizeSources(raw: ApiSource[] | unknown): Source[] {
  if (!Array.isArray(raw)) return [];

  return raw
    .map((item): Source | null => {
      const source = asRecord(item);
      const name = safeString(source.name);
      if (!name) return null;

      return {
        id: safeString(source.id),
        name,
        type: safeString(source.type),
        baseUrl: safeString(source.baseUrl),
        enabled: typeof source.enabled === "boolean" ? source.enabled : undefined,
      };
    })
    .filter((item): item is Source => item !== null);
}

export function normalizeFavoriteJobsResponse(raw: ApiFavoritesResponse | unknown): FavoriteJobsResponse {
  const data = asRecord(raw);
  const total = safeNumber(data.total, 0);
  const itemsRaw = Array.isArray(data.items) ? data.items : [];

  const items = itemsRaw
    .map((item) => normalizeJob(item))
    .filter((item) => item.id)
    .map((item) => ({
      id: item.id,
      title: item.title,
      company: item.company,
      location: item.location,
      postedAt: item.postedAt,
      sourceName: item.sourceName,
    }));

  return {
    total: Number.isFinite(total) ? total : items.length,
    items,
  };
}
