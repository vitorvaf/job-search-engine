type PaginationProps = {
  page: number;
  pageSize: number;
  total?: number;
  totalPages?: number;
  hasNext: boolean;
  onPageChange: (nextPage: number) => void;
};

export function Pagination({ page, pageSize, total, totalPages, hasNext, onPageChange }: PaginationProps) {
  const knownTotalPages = totalPages ?? (typeof total === "number" ? Math.ceil(total / pageSize) : undefined);
  const canNext = knownTotalPages ? page < knownTotalPages : hasNext;

  return (
    <nav aria-label="Paginação" className="mt-8 flex items-center justify-between">
      <button
        type="button"
        onClick={() => onPageChange(page - 1)}
        disabled={page <= 1}
        className="rounded-md border border-line px-3 py-2 text-sm font-medium text-ink transition hover:border-ink disabled:cursor-not-allowed disabled:opacity-40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ink focus-visible:ring-offset-2"
      >
        Anterior
      </button>
      <span className="text-sm text-muted">
        Página {page}
        {knownTotalPages ? ` de ${knownTotalPages}` : ""}
      </span>
      <button
        type="button"
        onClick={() => onPageChange(page + 1)}
        disabled={!canNext}
        className="rounded-md border border-line px-3 py-2 text-sm font-medium text-ink transition hover:border-ink disabled:cursor-not-allowed disabled:opacity-40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ink focus-visible:ring-offset-2"
      >
        Próxima
      </button>
    </nav>
  );
}
