import { classNames } from "@/lib/utils";

type TagPillProps = {
  label: string;
  className?: string;
};

export function TagPill({ label, className }: TagPillProps) {
  return (
    <span
      className={classNames(
        "inline-flex items-center rounded-full border border-line bg-white px-2.5 py-1 text-xs font-medium text-muted",
        className,
      )}
    >
      {label}
    </span>
  );
}
