export function SkeletonList() {
  return (
    <div className="space-y-3" aria-hidden="true">
      {Array.from({ length: 5 }).map((_, index) => (
        <div key={index} className="rounded-xl border border-line bg-white p-5 shadow-soft">
          <div className="h-5 w-2/3 animate-pulse rounded bg-line" />
          <div className="mt-3 h-4 w-1/3 animate-pulse rounded bg-line" />
          <div className="mt-4 h-4 w-1/2 animate-pulse rounded bg-line" />
        </div>
      ))}
    </div>
  );
}
