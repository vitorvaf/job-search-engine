import { type Job, type JobFilters } from "@/lib/types";
import { DEFAULT_PAGE_SIZE, DEFAULT_SORT, QUERY_PARAM_KEYS } from "@/lib/constants";

const ISO_DATE_RE = /^\d{4}-\d{2}-\d{2}(T.*)?$/;

export function classNames(...classes: Array<string | false | null | undefined>) {
  return classes.filter(Boolean).join(" ");
}

export function parseFiltersFromSearchParams(searchParams: URLSearchParams): JobFilters {
  const page = Number(searchParams.get("page") || "1");
  const pageSize = Number(searchParams.get("pageSize") || String(DEFAULT_PAGE_SIZE));

  return {
    page: Number.isFinite(page) && page > 0 ? page : 1,
    pageSize: Number.isFinite(pageSize) && pageSize > 0 ? pageSize : DEFAULT_PAGE_SIZE,
    q: searchParams.get("q") || undefined,
    tags: searchParams.get("tags") || undefined,
    workMode: searchParams.get("workMode") || undefined,
    seniority: searchParams.get("seniority") || undefined,
    employmentType: searchParams.get("employmentType") || undefined,
    sourceName: searchParams.get("sourceName") || undefined,
    company: searchParams.get("company") || undefined,
    location: searchParams.get("location") || undefined,
    postedFrom: normalizeIsoDate(searchParams.get("postedFrom")),
    sort: searchParams.get("sort") || DEFAULT_SORT,
  };
}

export function filtersToQueryString(filters: JobFilters): string {
  const query = new URLSearchParams();

  QUERY_PARAM_KEYS.forEach((key) => {
    const value = filters[key];
    if (value === undefined || value === null || value === "") return;
    query.set(key, String(value));
  });

  if (!query.has("page")) query.set("page", "1");
  if (!query.has("pageSize")) query.set("pageSize", String(DEFAULT_PAGE_SIZE));

  return query.toString();
}

export function getPostedFromDate(days: number): string {
  const now = new Date();
  now.setDate(now.getDate() - days);
  return now.toISOString();
}

export function normalizeIsoDate(value: string | null): string | undefined {
  if (!value) return undefined;
  if (!ISO_DATE_RE.test(value)) return undefined;
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? undefined : parsed.toISOString();
}

export function formatDate(date?: string): string {
  if (!date) return "Data não informada";
  const parsed = new Date(date);
  if (Number.isNaN(parsed.getTime())) return "Data não informada";
  return new Intl.DateTimeFormat("pt-BR", {
    dateStyle: "medium",
  }).format(parsed);
}

export function joinTags(tags?: Job["tags"]): string[] {
  if (!tags) return [];
  if (Array.isArray(tags)) return tags.filter(Boolean);
  return tags
    .split(",")
    .map((tag) => tag.trim())
    .filter(Boolean);
}

export function stripHtml(input: string): string {
  return input
    .replace(/<style[^>]*>[\s\S]*?<\/style>/gi, "")
    .replace(/<script[^>]*>[\s\S]*?<\/script>/gi, "")
    .replace(/<[^>]+>/g, "")
    .replace(/&nbsp;/g, " ")
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .trim();
}

export function sanitizeDescription(description?: string): string {
  if (!description) return "Descrição não disponível.";
  if (/<[a-z][\s\S]*>/i.test(description)) {
    return stripHtml(description);
  }
  return description;
}
