import Link from "next/link";
import { ArrowUpRight, Building2, MapPin, Clock3 } from "lucide-react";
import { type Job } from "@/lib/types";
import { formatDate, joinTags } from "@/lib/utils";
import { TagPill } from "@/components/tag-pill";

type JobCardProps = {
  job: Job;
};

export function JobCard({ job }: JobCardProps) {
  const tags = joinTags(job.tags).slice(0, 4);

  return (
    <article className="rounded-xl border border-line bg-white p-5 shadow-soft transition hover:border-ink/25">
      <Link href={`/vagas/${job.id}`} className="group block focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ink focus-visible:ring-offset-2">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h2 className="font-serif text-2xl leading-tight text-ink transition group-hover:text-accent">{job.title ?? "Título não informado"}</h2>
            <div className="mt-3 flex flex-wrap gap-3 text-sm text-muted">
              <span className="inline-flex items-center gap-1.5">
                <Building2 className="h-4 w-4" aria-hidden="true" />
                {job.company ?? "Empresa não informada"}
              </span>
              <span className="inline-flex items-center gap-1.5">
                <MapPin className="h-4 w-4" aria-hidden="true" />
                {job.location ?? "Local não informado"}
              </span>
              <span className="inline-flex items-center gap-1.5">
                <Clock3 className="h-4 w-4" aria-hidden="true" />
                {formatDate(job.postedAt)}
              </span>
            </div>
          </div>
          <ArrowUpRight className="mt-1 h-5 w-5 text-muted transition group-hover:text-ink" aria-hidden="true" />
        </div>

        <div className="mt-4 flex flex-wrap gap-2">
          {[job.workMode, job.seniority, job.employmentType, job.sourceName]
            .filter((value): value is string => Boolean(value))
            .map((meta) => (
              <TagPill key={meta} label={meta} />
            ))}
          {tags.map((tag) => (
            <TagPill key={tag} label={`#${tag}`} />
          ))}
        </div>
      </Link>
    </article>
  );
}
