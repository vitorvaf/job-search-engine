export const FILTER_OPTIONS = {
  workMode: ["", "Remote", "Hybrid", "Onsite"],
  seniority: ["", "Intern", "Junior", "Mid", "Senior", "Lead", "Principal"],
  employmentType: ["", "CLT", "PJ", "Contract", "Temporary", "Internship"],
  sort: [
    { value: "recent", label: "Mais recentes" },
    { value: "postedAt:asc", label: "Mais antigas" },
    { value: "capturedAt:desc", label: "Capturadas recentemente" },
  ],
} as const;

export const PAGE_SIZE_OPTIONS = [10, 20, 30] as const;
export const DEFAULT_PAGE_SIZE = 20;
export const DEFAULT_SORT = "recent";
export const QUERY_PARAM_KEYS = [
  "page",
  "pageSize",
  "q",
  "tags",
  "workMode",
  "seniority",
  "employmentType",
  "sourceName",
  "company",
  "location",
  "postedFrom",
  "sort",
] as const;
