# GitHub Copilot — Repository Instructions

## Project overview

`job-search-engine` aggregates public job postings, normalizes them into a single `JobPosting` domain model, deduplicates them, stores them in PostgreSQL, indexes them in Meilisearch, and serves them through a REST API consumed by a Next.js frontend.

```text
[Sources] -> Worker (fetch -> parse -> normalize -> dedupe) -> PostgreSQL
                                                        -> Meilisearch
Browser -> Next.js Route Handlers (BFF) -> Jobs.Api -> Meilisearch / PostgreSQL
```

## Repository structure

```text
src/backend/
  Jobs.Domain/         Canonical domain model and enums
  Jobs.Infrastructure/ EF Core, ingestion sources, parsers, MeiliClient, IngestionPipeline
  Jobs.Api/            ASP.NET Core Minimal API; routes live in Program.cs
  Jobs.Worker/         BackgroundService orchestrating ingestion
  Jobs.Tests/          xUnit tests
  tests/fixtures/      HTML/JSON fixtures copied to test output

src/frontend/
  app/                 Next.js App Router pages and BFF route handlers
  components/          UI components
  lib/
    api-proxy.ts       Query sanitization and backend fetch plumbing
    normalizers.ts     Raw backend payload -> normalized frontend model
    types.ts           Raw API shapes + normalized frontend models
    constants.ts       Filter options and defaults

docs/                  Architecture, ingestion, contracts, and test strategy
```

## Domain model

`JobPosting` is the canonical domain object. Always use its nested value objects; never flatten them.

```csharp
// Value objects
JobSourceRef  { Name, SourceType Type, Url, SourceJobId? }
CompanyRef    { Name, Website?, Industry? }
LocationRef   { Country?, State?, City? }
SalaryRange   { Min?, Max?, Currency?, Period? }
DedupeInfo    { Fingerprint, ClusterId? }

// Enums (authoritative values — do not invent others)
WorkMode       : Unknown | Remote | Hybrid | Onsite
Seniority      : Unknown | Intern | Junior | Mid | Senior | Staff | Lead | Principal
EmploymentType : Unknown | CLT | PJ | Contractor | Internship | Temporary
JobStatus      : Unknown | Active | Expired
SourceType     : Unknown | LinkedIn | Greenhouse | Lever | Indeed | InfoJobs | Vagas
               | CareersPage | JsonLd | CorporateCareers | Gupy | Workday | Fixture
```

## Backend conventions

- Use .NET 8 and `System.Text.Json`.
- `Jobs.Api` uses Minimal API in `Program.cs`. Do not create MVC controllers.
- All outbound HTTP goes through the named `"Sources"` `HttpClient`.
- `IJobSource` returns `IAsyncEnumerable<JobPosting>` from `FetchAsync(...)`.
- `ParsedSourceJob` is a parser/helper stage, not the final ingestion contract.
- Reuse `Fingerprint` for dedupe metadata; do not compute hashes inline.
- Schema changes go in `src/backend/Jobs.Infrastructure/Data/schema.sql`; do not add EF migrations.
- If a field changes across ingestion/search/API, review domain models, entities, mapping, Meilisearch indexing, and API responses together.

## Adding a new source

Do not assume every new source needs a brand-new `IJobSource`.

Decision order:
1. Check if the source fits an existing family such as `CorporateCareers`, `JsonLd`, or `GupyCompanies`.
2. Reuse existing parser families when possible (`InfoJobsHtmlParser`, `WorkdayJobsJsonParser`, `GupyJobsJsonParser`, `JsonLdHtmlParser`, `TotvsHtmlParser`).
3. Only create a new connector when fetch/parse behavior is materially different.

Expected change set:
- ingestion code or parser changes under `Jobs.Infrastructure/Ingestion/`
- `AppOptions` and `appsettings*.json` updates in both `Jobs.Api` and `Jobs.Worker` when configuration is required
- fixture(s) in `src/backend/tests/fixtures/`
- xUnit coverage in `src/backend/Jobs.Tests/Ingestion/`
- docs or samples updates under `docs/` when useful

## Frontend conventions

- Use the Next.js App Router only.
- The browser never calls `Jobs.Api` directly. All browser traffic goes through `src/frontend/app/api/`.
- `src/frontend/lib/types.ts` contains the normalized models used by the UI plus documented raw API shapes.
- `src/frontend/lib/normalizers.ts` adapts backend payloads for the UI.
- Changes to filters, sort, pagination, or enum values must review backend API, BFF route handlers, `api-proxy.ts`, `normalizers.ts`, `types.ts`, `constants.ts`, and the affected components together.
- `BACKEND_URL` is server-side only.

## Tests and validation

- Backend tests use xUnit.
- The current automated suite is strongest around parser, normalizer, fingerprint, and fixture-based validation coverage.
- Fixtures live in `src/backend/tests/fixtures/`.
- Frontend validation currently relies on `npm run lint` and `npm run build`.
- Do not claim API, worker, pipeline, BFF, or frontend test coverage unless it actually exists.

## What Copilot should not do

- Do not add EF migrations.
- Do not add `Newtonsoft.Json`.
- Do not create MVC controllers, Razor Pages, or a frontend `pages/` directory.
- Do not call `Jobs.Api` directly from browser code.
- Do not invent frontend contract fields without updating `normalizers.ts`, `types.ts`, and the UI.
- Do not assume every source change is a new connector.

## Local run defaults

- API: `http://localhost:5004`
- Swagger: `http://localhost:5004/swagger`
- Frontend: `http://localhost:3000`
- Infra: `docker compose up -d`
