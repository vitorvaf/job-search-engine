"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { EmptyState } from "@/components/empty-state";
import { FiltersBar } from "@/components/filters-bar";
import { JobCard } from "@/components/job-card";
import { Pagination } from "@/components/pagination";
import { SkeletonList } from "@/components/skeleton-list";
import { DEFAULT_PAGE_SIZE, DEFAULT_SORT } from "@/lib/constants";
import { type Job, type JobFilters, type JobsResponse, type Source } from "@/lib/types";
import { filtersToQueryString, parseFiltersFromSearchParams } from "@/lib/utils";

export function JobsListPage() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const initialFilters = useMemo(() => parseFiltersFromSearchParams(new URLSearchParams(searchParams.toString())), [searchParams]);

  const [filters, setFilters] = useState<JobFilters>({
    ...initialFilters,
    page: initialFilters.page || 1,
    pageSize: initialFilters.pageSize || DEFAULT_PAGE_SIZE,
    sort: initialFilters.sort || DEFAULT_SORT,
  });
  const [debouncedQ, setDebouncedQ] = useState(filters.q ?? "");
  const [jobs, setJobs] = useState<Job[]>([]);
  const [meta, setMeta] = useState<Pick<JobsResponse, "page" | "pageSize" | "total" | "totalPages">>({
    page: filters.page,
    pageSize: filters.pageSize,
  });
  const [sources, setSources] = useState<Source[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [retryToken, setRetryToken] = useState(0);

  useEffect(() => {
    const next = parseFiltersFromSearchParams(new URLSearchParams(searchParams.toString()));
    setFilters((prev) => ({
      ...prev,
      ...next,
      page: next.page || 1,
      pageSize: next.pageSize || DEFAULT_PAGE_SIZE,
      sort: next.sort || DEFAULT_SORT,
    }));
  }, [searchParams]);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedQ(filters.q ?? "");
    }, 400);

    return () => window.clearTimeout(timer);
  }, [filters.q]);

  const effectiveFilters = useMemo(
    () => ({
      ...filters,
      q: debouncedQ || undefined,
      page: filters.page || 1,
      pageSize: filters.pageSize || DEFAULT_PAGE_SIZE,
      sort: filters.sort || DEFAULT_SORT,
    }),
    [debouncedQ, filters],
  );

  useEffect(() => {
    const query = filtersToQueryString(effectiveFilters);
    const current = searchParams.toString();
    if (query === current) return;
    router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
  }, [effectiveFilters, pathname, router, searchParams]);

  const fetchJobs = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const query = filtersToQueryString(effectiveFilters);
      const response = await fetch(`/api/jobs?${query}`, { cache: "no-store" });
      const payload = (await response.json()) as JobsResponse | { message?: string };

      if (!response.ok) {
        const message = "message" in payload ? payload.message : undefined;
        throw new Error(message || "Não foi possível carregar as vagas.");
      }

      const data = payload as JobsResponse;
      const items = Array.isArray(data.items) ? data.items : [];

      setJobs(items);
      setMeta({ page: data.page, pageSize: data.pageSize, total: data.total, totalPages: data.totalPages });
    } catch (fetchError) {
      setError(fetchError instanceof Error ? fetchError.message : "Erro inesperado ao carregar vagas.");
      setJobs([]);
    } finally {
      setLoading(false);
    }
  }, [effectiveFilters]);

  useEffect(() => {
    void fetchJobs();
  }, [fetchJobs, retryToken]);

  useEffect(() => {
    let cancelled = false;

    async function fetchSources() {
      try {
        const response = await fetch("/api/sources");
        if (!response.ok) return;
        const payload = (await response.json()) as Source[];
        if (!cancelled && Array.isArray(payload)) {
          setSources(payload);
        }
      } catch {
        if (!cancelled) {
          setSources([]);
        }
      }
    }

    void fetchSources();
    return () => {
      cancelled = true;
    };
  }, []);

  const handleChangeFilters = (changes: Partial<JobFilters>) => {
    setFilters((prev) => ({ ...prev, ...changes }));
  };

  const handleClearFilters = () => {
    setFilters({ page: 1, pageSize: DEFAULT_PAGE_SIZE, sort: DEFAULT_SORT });
  };

  const hasNext = meta.totalPages ? meta.page < meta.totalPages : jobs.length >= meta.pageSize;

  return (
    <div className="space-y-6">
      <header className="space-y-2">
        <h1 className="font-serif text-4xl leading-tight text-ink md:text-5xl">Vagas de tecnologia sem ruído</h1>
        <p className="max-w-2xl text-sm text-muted">Encontre oportunidades reais com filtros objetivos e leitura limpa.</p>
      </header>

      <FiltersBar filters={filters} sources={sources} onChange={handleChangeFilters} onClear={handleClearFilters} />

      {loading ? <SkeletonList /> : null}

      {!loading && error ? (
        <EmptyState
          title="Não foi possível carregar"
          description={error}
          actionLabel="Tentar novamente"
          onAction={() => setRetryToken((value) => value + 1)}
        />
      ) : null}

      {!loading && !error && jobs.length === 0 ? (
        <EmptyState
          title="Nenhuma vaga encontrada"
          description="Ajuste os filtros ou limpe a busca para ver mais resultados."
          actionLabel="Limpar filtros"
          onAction={handleClearFilters}
        />
      ) : null}

      {!loading && !error && jobs.length > 0 ? (
        <section className="space-y-3" aria-live="polite">
          {jobs.map((job) => (
            <JobCard key={job.id} job={job} />
          ))}
          <Pagination
            page={meta.page}
            pageSize={meta.pageSize}
            total={meta.total}
            totalPages={meta.totalPages}
            hasNext={hasNext}
            onPageChange={(nextPage) => handleChangeFilters({ page: nextPage })}
          />
        </section>
      ) : null}
    </div>
  );
}
