import { type Job, type JobsResponse, type Source } from "@/lib/types";

export async function fetchJobs(query: string): Promise<JobsResponse> {
  const response = await fetch(`/api/jobs?${query}`, { cache: "no-store" });
  if (!response.ok) {
    throw new Error("Não foi possível carregar vagas.");
  }
  return (await response.json()) as JobsResponse;
}

export async function fetchJobById(id: string): Promise<Job> {
  const response = await fetch(`/api/jobs/${id}`, { cache: "no-store" });
  if (!response.ok) {
    throw new Error("Não foi possível carregar vaga.");
  }
  return (await response.json()) as Job;
}

export async function fetchSources(): Promise<Source[]> {
  const response = await fetch("/api/sources", { cache: "no-store" });
  if (!response.ok) {
    return [];
  }
  return (await response.json()) as Source[];
}
