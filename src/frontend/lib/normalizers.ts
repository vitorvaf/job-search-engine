import { DEFAULT_PAGE_SIZE } from "@/lib/constants";
import { type Job, type JobsResponse, type Source } from "@/lib/types";

function safeString(value: unknown): string | undefined {
  return typeof value === "string" && value.trim() ? value : undefined;
}

function safeArray(value: unknown): string[] | undefined {
  if (!Array.isArray(value)) return undefined;
  const out = value.filter((item): item is string => typeof item === "string" && item.trim().length > 0);
  return out.length > 0 ? out : undefined;
}

function safeNumber(value: unknown, fallback: number): number {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

export function normalizeJob(raw: unknown): Job {
  const item = typeof raw === "object" && raw !== null ? (raw as Record<string, unknown>) : {};
  const companyObj = typeof item.company === "object" && item.company !== null ? (item.company as Record<string, unknown>) : undefined;
  const sourceObj = typeof item.source === "object" && item.source !== null ? (item.source as Record<string, unknown>) : undefined;
  const locationObj = typeof item.location === "object" && item.location !== null ? (item.location as Record<string, unknown>) : undefined;

  return {
    id: safeString(item.id) ?? "",
    title: safeString(item.title),
    company: safeString(item.company) ?? safeString(companyObj?.name),
    location: safeString(item.locationText) ?? safeString(item.location) ?? safeString(locationObj?.city),
    workMode: safeString(item.workMode),
    seniority: safeString(item.seniority),
    employmentType: safeString(item.employmentType),
    tags: safeArray(item.tags) ?? safeString(item.tags),
    description: safeString(item.description) ?? safeString(item.descriptionText),
    applyUrl: safeString(item.applyUrl) ?? safeString(sourceObj?.url),
    sourceName: safeString(item.sourceName) ?? safeString(sourceObj?.name),
    postedAt: safeString(item.postedAt),
  };
}

export function normalizeJobsResponse(raw: unknown, page = 1, pageSize = DEFAULT_PAGE_SIZE): JobsResponse {
  const data = typeof raw === "object" && raw !== null ? (raw as Record<string, unknown>) : {};

  let itemsRaw: unknown = data.items;
  let currentPage = safeNumber(data.page, page);
  let currentPageSize = safeNumber(data.pageSize, pageSize);
  let total = typeof data.total === "number" ? data.total : undefined;
  let totalPages = typeof data.totalPages === "number" ? data.totalPages : undefined;

  if (!Array.isArray(itemsRaw) && Array.isArray(data.data)) {
    itemsRaw = data.data;
    const meta = typeof data.meta === "object" && data.meta !== null ? (data.meta as Record<string, unknown>) : {};
    currentPage = safeNumber(meta.page, page);
    currentPageSize = safeNumber(meta.pageSize, pageSize);
    total = typeof meta.total === "number" ? meta.total : total;
    totalPages = typeof meta.totalPages === "number" ? meta.totalPages : totalPages;
  }

  const items = Array.isArray(itemsRaw)
    ? itemsRaw.map((item) => normalizeJob(item)).filter((item) => item.id)
    : [];

  return {
    items,
    page: currentPage,
    pageSize: currentPageSize,
    total,
    totalPages,
  };
}

export function normalizeSources(raw: unknown): Source[] {
  if (!Array.isArray(raw)) return [];

  return raw
    .map((item) => {
      const source = typeof item === "object" && item !== null ? (item as Record<string, unknown>) : {};
      const name = safeString(source.name);
      if (!name) return null;

      return {
        id: safeString(source.id),
        name,
        type: safeString(source.type),
        baseUrl: safeString(source.baseUrl),
        enabled: typeof source.enabled === "boolean" ? source.enabled : undefined,
      } satisfies Source;
    })
    .filter((item): item is Source => item !== null);
}
