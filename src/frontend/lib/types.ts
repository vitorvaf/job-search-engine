export type ApiJobCompany = {
  name?: string;
  website?: string;
  industry?: string;
};

export type ApiJobLocation = {
  country?: string;
  state?: string;
  city?: string;
};

export type ApiJobSource = {
  name?: string;
  type?: string;
  url?: string;
  sourceJobId?: string;
};

export type ApiJobSalary = {
  min?: number | null;
  max?: number | null;
  currency?: string;
  period?: string;
};

export type ApiJobListItem = {
  id: string;
  title?: string;
  company?: ApiJobCompany | string;
  locationText?: string;
  location?: ApiJobLocation | string;
  workMode?: string;
  seniority?: string;
  employmentType?: string;
  tags?: string[] | string;
  postedAt?: string;
  capturedAt?: string;
  source?: ApiJobSource;
  sourceName?: string;
  applyUrl?: string;
};

export type ApiJobDetail = ApiJobListItem & {
  salary?: ApiJobSalary | null;
  descriptionText?: string;
  description?: string;
  languages?: string[];
  lastSeenAt?: string;
  status?: string;
  dedupe?: {
    fingerprint?: string;
    clusterId?: string | null;
  };
  metadata?: unknown;
};

export type ApiJobsResponse = {
  items?: ApiJobListItem[];
  page?: number;
  pageSize?: number;
  total?: number;
  totalPages?: number;
  data?: ApiJobListItem[];
  meta?: {
    page?: number;
    pageSize?: number;
    total?: number;
    totalPages?: number;
  };
};

export type ApiSource = {
  id?: string;
  name: string;
  type?: string;
  baseUrl?: string;
  enabled?: boolean;
};

export type Job = {
  id: string;
  title?: string;
  company?: string;
  location?: string;
  workMode?: string;
  seniority?: string;
  employmentType?: string;
  tags?: string[];
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

export type ApiFavoritesResponse = {
  total?: number;
  items?: ApiJobListItem[];
};

export type FavoriteJobsResponse = {
  total: number;
  items: FavoriteJob[];
};
