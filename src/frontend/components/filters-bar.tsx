"use client";

import { FILTER_OPTIONS, PAGE_SIZE_OPTIONS } from "@/lib/constants";
import { type JobFilters, type Source } from "@/lib/types";
import { getPostedFromDate } from "@/lib/utils";

type FiltersBarProps = {
  filters: JobFilters;
  sources: Source[];
  onChange: (changes: Partial<JobFilters>) => void;
  onClear: () => void;
};

const postedFromOptions = [
  { label: "7 dias", days: 7 },
  { label: "30 dias", days: 30 },
  { label: "90 dias", days: 90 },
];

export function FiltersBar({ filters, sources, onChange, onClear }: FiltersBarProps) {
  const handlePostedFrom = (days: number) => {
    onChange({ postedFrom: getPostedFromDate(days), page: 1 });
  };

  return (
    <section className="rounded-xl border border-line bg-white p-4 md:p-5">
      <h2 className="font-serif text-lg text-ink">Filtrar vagas</h2>
      <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-2">
        <label className="block text-sm text-muted">
          Busca
          <input
            type="search"
            value={filters.q ?? ""}
            onChange={(event) => onChange({ q: event.target.value, page: 1 })}
            placeholder="Ex.: React, .NET, dados"
            className="mt-1 w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink outline-none transition placeholder:text-muted/70 focus:border-ink"
          />
        </label>

        <label className="block text-sm text-muted">
          Tags (separadas por vírgula)
          <input
            type="text"
            value={filters.tags ?? ""}
            onChange={(event) => onChange({ tags: event.target.value, page: 1 })}
            placeholder="dotnet, react"
            className="mt-1 w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink outline-none transition placeholder:text-muted/70 focus:border-ink"
          />
        </label>

        <label className="block text-sm text-muted">
          Empresa
          <input
            type="text"
            value={filters.company ?? ""}
            onChange={(event) => onChange({ company: event.target.value, page: 1 })}
            placeholder="Nome da empresa"
            className="mt-1 w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink outline-none transition placeholder:text-muted/70 focus:border-ink"
          />
        </label>

        <label className="block text-sm text-muted">
          Localização
          <input
            type="text"
            value={filters.location ?? ""}
            onChange={(event) => onChange({ location: event.target.value, page: 1 })}
            placeholder="Cidade ou estado"
            className="mt-1 w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink outline-none transition placeholder:text-muted/70 focus:border-ink"
          />
        </label>

        <label className="block text-sm text-muted">
          Modalidade
          <select
            value={filters.workMode ?? ""}
            onChange={(event) => onChange({ workMode: event.target.value || undefined, page: 1 })}
            className="mt-1 w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink outline-none transition focus:border-ink"
          >
            {FILTER_OPTIONS.workMode.map((option) => (
              <option key={option || "all"} value={option}>
                {option || "Todas"}
              </option>
            ))}
          </select>
        </label>

        <label className="block text-sm text-muted">
          Senioridade
          <select
            value={filters.seniority ?? ""}
            onChange={(event) => onChange({ seniority: event.target.value || undefined, page: 1 })}
            className="mt-1 w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink outline-none transition focus:border-ink"
          >
            {FILTER_OPTIONS.seniority.map((option) => (
              <option key={option || "all"} value={option}>
                {option || "Todas"}
              </option>
            ))}
          </select>
        </label>

        <label className="block text-sm text-muted">
          Tipo de vínculo
          <select
            value={filters.employmentType ?? ""}
            onChange={(event) => onChange({ employmentType: event.target.value || undefined, page: 1 })}
            className="mt-1 w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink outline-none transition focus:border-ink"
          >
            {FILTER_OPTIONS.employmentType.map((option) => (
              <option key={option || "all"} value={option}>
                {option || "Todos"}
              </option>
            ))}
          </select>
        </label>

        <label className="block text-sm text-muted">
          Fonte
          <select
            value={filters.sourceName ?? ""}
            onChange={(event) => onChange({ sourceName: event.target.value || undefined, page: 1 })}
            className="mt-1 w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink outline-none transition focus:border-ink"
          >
            <option value="">Todas</option>
            {sources.map((source) => (
              <option key={source.name} value={source.name}>
                {source.name}
              </option>
            ))}
          </select>
        </label>

        <label className="block text-sm text-muted">
          Ordenação
          <select
            value={filters.sort ?? "recent"}
            onChange={(event) => onChange({ sort: event.target.value || "recent", page: 1 })}
            className="mt-1 w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink outline-none transition focus:border-ink"
          >
            {FILTER_OPTIONS.sort.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>

        <label className="block text-sm text-muted">
          Itens por página
          <select
            value={String(filters.pageSize)}
            onChange={(event) => onChange({ pageSize: Number(event.target.value), page: 1 })}
            className="mt-1 w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink outline-none transition focus:border-ink"
          >
            {PAGE_SIZE_OPTIONS.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </label>
      </div>

      <div className="mt-4 flex flex-wrap items-center gap-2">
        <span className="text-xs uppercase tracking-wide text-muted">Publicadas em</span>
        {postedFromOptions.map((option) => (
          <button
            key={option.days}
            type="button"
            onClick={() => handlePostedFrom(option.days)}
            className="rounded-full border border-line px-3 py-1 text-xs text-ink transition hover:border-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ink focus-visible:ring-offset-2"
          >
            {option.label}
          </button>
        ))}
        <button
          type="button"
          onClick={() => onChange({ postedFrom: undefined, page: 1 })}
          className="text-xs font-medium text-muted underline-offset-2 hover:underline"
        >
          Remover data
        </button>
      </div>

      <div className="mt-5 flex justify-end">
        <button
          type="button"
          onClick={onClear}
          className="rounded-md border border-line px-3 py-2 text-sm font-medium text-ink transition hover:border-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ink focus-visible:ring-offset-2"
        >
          Limpar filtros
        </button>
      </div>
    </section>
  );
}
