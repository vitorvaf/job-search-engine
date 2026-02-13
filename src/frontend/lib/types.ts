export type Job = {
  id: string;
  title?: string;
  company?: string;
  location?: string;
  workMode?: string;
  seniority?: string;
  employmentType?: string;
  tags?: string[] | string;
  description?: string;
  applyUrl?: string;
  sourceName?: string;
  postedAt?: string;
};

export type JobsResponse = {
  items: Job[];
  page: number;
  pageSize: number;
  total?: number;
  totalPages?: number;
};

export type Source = {
  id?: string;
  name: string;
  type?: string;
  baseUrl?: string;
  enabled?: boolean;
};

export type JobFilters = {
  page: number;
  pageSize: number;
  q?: string;
  tags?: string;
  workMode?: string;
  seniority?: string;
  employmentType?: string;
  sourceName?: string;
  company?: string;
  location?: string;
  postedFrom?: string;
  sort?: string;
};

export type FavoriteJob = {
  id: string;
  title?: string;
  company?: string;
  location?: string;
  postedAt?: string;
  sourceName?: string;
};
