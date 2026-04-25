import { type NextRequest } from "next/server";
import { DEFAULT_PAGE_SIZE } from "@/lib/constants";
import { normalizeEmploymentTypeValue, normalizeSortValue } from "@/lib/utils";

const TEXT_QUERY_KEYS = [
  "q",
  "tags",
  "workMode",
  "seniority",
  "employmentType",
  "sourceName",
  "company",
  "location",
  "postedFrom",
  "sort",
] as const;

export function getBackendUrl() {
  const backendUrl = process.env.BACKEND_URL;
  if (!backendUrl) {
    throw new Error("BACKEND_URL is not configured.");
  }
  return backendUrl.replace(/\/$/, "");
}

export function getInternalApiKey() {
  const key = process.env.BACKEND_INTERNAL_API_KEY;
  if (!key) {
    throw new Error("BACKEND_INTERNAL_API_KEY is not configured.");
  }

  return key;
}

export function getInternalApiHeaders(extraHeaders?: Record<string, string>) {
  return {
    "Content-Type": "application/json",
    "X-Internal-Api-Key": getInternalApiKey(),
    ...(extraHeaders ?? {}),
  };
}

function parsePage(value: string | null, fallback: number) {
  const parsed = Number(value ?? "");
  if (!Number.isFinite(parsed) || parsed <= 0) return fallback;
  return Math.floor(parsed);
}

function sanitizeText(value: string | null, maxLength: number) {
  if (!value) return undefined;
  const trimmed = value.trim();
  if (!trimmed) return undefined;
  return trimmed.slice(0, maxLength);
}

function sanitizePostedFrom(value: string | null) {
  if (!value) return undefined;
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return undefined;
  return parsed.toISOString();
}

function normalizeQueryValue(key: (typeof TEXT_QUERY_KEYS)[number], value: string | null) {
  if (key === "postedFrom") return sanitizePostedFrom(value);
  if (key === "employmentType") return normalizeEmploymentTypeValue(value);
  if (key === "sort") return normalizeSortValue(value);
  return sanitizeText(value, 200);
}

export function buildJobsQueryParams(request: NextRequest) {
  const source = request.nextUrl.searchParams;
  const query = new URLSearchParams();

  const page = parsePage(source.get("page"), 1);
  const pageSize = Math.min(parsePage(source.get("pageSize"), DEFAULT_PAGE_SIZE), 100);

  query.set("page", String(page));
  query.set("pageSize", String(pageSize));

  TEXT_QUERY_KEYS.forEach((key) => {
    const value = normalizeQueryValue(key, source.get(key));
    if (!value) return;
    query.set(key, value);
  });

  return { query, page, pageSize };
}

export async function safeJson<T>(response: Response): Promise<T | null> {
  try {
    return (await response.json()) as T;
  } catch {
    return null;
  }
}
