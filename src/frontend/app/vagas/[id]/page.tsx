import Link from "next/link";
import { headers } from "next/headers";
import { notFound } from "next/navigation";
import { ArrowLeft, Building2, MapPin, BriefcaseBusiness, Clock3, ExternalLink } from "lucide-react";
import { CopyLinkButton } from "@/components/copy-link-button";
import { FavoriteButton } from "@/components/favorite-button";
import { TagPill } from "@/components/tag-pill";
import { type Job } from "@/lib/types";
import { formatDate, joinTags, sanitizeDescription } from "@/lib/utils";

type JobPageProps = {
  params: { id: string };
};

async function getBaseUrl() {
  const headerList = await headers();
  const host = headerList.get("x-forwarded-host") ?? headerList.get("host");
  const protocol = headerList.get("x-forwarded-proto") ?? "http";

  if (host) {
    return `${protocol}://${host}`;
  }

  return process.env.NEXT_PUBLIC_SITE_URL || "http://localhost:3000";
}

async function getJob(id: string): Promise<Job | null> {
  const baseUrl = await getBaseUrl();
  const response = await fetch(`${baseUrl}/api/jobs/${id}`, {
    cache: "no-store",
  });

  if (response.status === 404) return null;
  if (!response.ok) {
    throw new Error("Não foi possível carregar a vaga.");
  }

  const payload = (await response.json()) as Job;
  return payload?.id ? payload : null;
}

export default async function JobDetailsPage({ params }: JobPageProps) {
  const { id } = await params;
  const job = await getJob(id);

  if (!job) {
    notFound();
  }

  const tags = joinTags(job.tags);

  return (
    <article className="space-y-7">
      <Link href="/" className="inline-flex items-center gap-2 text-sm font-medium text-muted hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ink focus-visible:ring-offset-2">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Voltar para vagas
      </Link>

      <header className="space-y-3">
        <h1 className="font-serif text-4xl leading-tight text-ink md:text-5xl">{job.title ?? "Vaga"}</h1>
        <div className="flex flex-wrap gap-3 text-sm text-muted">
          <span className="inline-flex items-center gap-1.5">
            <Building2 className="h-4 w-4" aria-hidden="true" />
            {job.company ?? "Empresa não informada"}
          </span>
          <span className="inline-flex items-center gap-1.5">
            <MapPin className="h-4 w-4" aria-hidden="true" />
            {job.location ?? "Local não informado"}
          </span>
          <span className="inline-flex items-center gap-1.5">
            <BriefcaseBusiness className="h-4 w-4" aria-hidden="true" />
            {[job.workMode, job.seniority, job.employmentType].filter(Boolean).join(" · ") || "Detalhes não informados"}
          </span>
          <span className="inline-flex items-center gap-1.5">
            <Clock3 className="h-4 w-4" aria-hidden="true" />
            {formatDate(job.postedAt)}
          </span>
        </div>

        <div className="flex flex-wrap gap-2">
          {job.sourceName ? <TagPill label={job.sourceName} /> : null}
          {tags.map((tag) => (
            <TagPill key={tag} label={`#${tag}`} />
          ))}
        </div>
      </header>

      <section className="space-y-4 rounded-xl border border-line bg-white p-5 shadow-soft">
        <h2 className="font-serif text-2xl text-ink">Descrição</h2>
        <p className="whitespace-pre-wrap text-sm leading-relaxed text-muted">{sanitizeDescription(job.description)}</p>
      </section>

      <section className="flex flex-wrap gap-2">
        {job.applyUrl ? (
          <a
            href={job.applyUrl}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-2 rounded-md border border-ink bg-ink px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ink focus-visible:ring-offset-2"
          >
            <ExternalLink className="h-4 w-4" aria-hidden="true" />
            Abrir vaga original
          </a>
        ) : null}
        <FavoriteButton job={job} />
        <CopyLinkButton path={`/vagas/${job.id}`} />
      </section>
    </article>
  );
}
