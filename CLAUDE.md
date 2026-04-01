# job-search-engine

## Project mission

`job-search-engine` aggregates public job postings, normalizes them into a single domain model, stores them in PostgreSQL, indexes them in Meilisearch, and serves them through a REST API consumed by a Next.js frontend.

```text
[Sources] -> Worker (fetch -> parse -> normalize -> dedupe) -> PostgreSQL
                                                        -> Meilisearch
Browser -> Next.js Route Handlers (BFF) -> Jobs.Api -> Meilisearch / PostgreSQL
```

## Source of truth

- `/docs` is the authoritative specification for the system.
- `docs/PROJECT_RULES.md` is the project constitution.
- If docs, prompt files, and code disagree, prefer the current implementation under `src/` and then update the guidance files.

## Repository map

- `src/backend/Jobs.Domain/`: canonical domain model.
- `src/backend/Jobs.Infrastructure/`: ingestion connectors, parsers, EF Core, Meilisearch integration.
- `src/backend/Jobs.Api/`: ASP.NET Core Minimal API.
- `src/backend/Jobs.Worker/`: background ingestion worker.
- `src/backend/Jobs.Tests/`: xUnit tests.
- `src/backend/tests/fixtures/`: HTML/JSON fixtures copied into test output.
- `src/frontend/`: Next.js App Router frontend and BFF route handlers.

## Key code paths

- Source orchestration: `src/backend/Jobs.Worker/Worker.cs` and `src/backend/Jobs.Infrastructure/Ingestion/IngestionPipeline.cs`
- Source families and parsers: `src/backend/Jobs.Infrastructure/Ingestion/`
- Dedupe fingerprinting: `src/backend/Jobs.Infrastructure/Ingestion/Fingerprint.cs`
- Persistence and mapping: `src/backend/Jobs.Infrastructure/Data/` and `src/backend/Jobs.Infrastructure/Data/Mapping/MappingExtensions.cs`
- Search indexing: `src/backend/Jobs.Infrastructure/Search/MeiliClient.cs`
- API contract: `src/backend/Jobs.Api/Program.cs`
- Frontend BFF boundary: `src/frontend/app/api/` and `src/frontend/lib/api-proxy.ts`
- Frontend normalization and view models: `src/frontend/lib/normalizers.ts` and `src/frontend/lib/types.ts`

## Common commands

- Infra: `cp .env.example .env && docker compose up -d`
- Backend API: `dotnet run --project src/backend/Jobs.Api`
- Worker: `dotnet run --project src/backend/Jobs.Worker`
- Worker once for a source: `dotnet run --project src/backend/Jobs.Worker -- --run-once --source=InfoJobs`
- Backend tests: `dotnet test src/backend/Jobs.sln`
- Frontend lint: `cd src/frontend && npm run lint`
- Frontend build: `cd src/frontend && npm run build`

## Cross-cutting rules

- Backend stays on .NET 8 and uses `System.Text.Json`.
- `Jobs.Api` uses Minimal APIs in `Program.cs`; do not add MVC controllers or Razor Pages.
- All outbound HTTP goes through the named `"Sources"` `HttpClient`.
- `IJobSource` implementations return `IAsyncEnumerable<JobPosting>` from `FetchAsync(...)`.
- `ParsedSourceJob` is a parser/helper stage only. Final sources still emit `JobPosting`.
- Build `JobPosting` using its nested value objects (`JobSourceRef`, `CompanyRef`, `LocationRef`, `SalaryRange`, `DedupeInfo`) rather than inventing flat duplicates.
- Use the injected `Fingerprint` service for dedupe fingerprints; do not recompute hashes ad hoc.
- Database schema is managed in `src/backend/Jobs.Infrastructure/Data/schema.sql`; do not add EF migrations.
- Frontend uses the App Router only; the browser never calls `Jobs.Api` directly.
- Route Handlers in `src/frontend/app/api/` are the BFF boundary for browser traffic.
- `src/frontend/lib/types.ts` defines the normalized frontend model. The raw backend payload is defined by `src/backend/Jobs.Api/Program.cs` and documented in `docs/07_api_contracts.md`.
- If filters, enums, sorting, or pagination change, review backend API, BFF route handlers, `api-proxy.ts`, `normalizers.ts`, `types.ts`, `constants.ts`, and the affected UI together.
- Prefer existing source families (`CorporateCareers`, `JsonLd`, `GupyCompanies`) before adding a brand-new connector.
- New ingestion sources should ship with fixtures, tests, and docs updates together.

## Keep guidance aligned

- `.claude/` contains Claude Code memory, rules, and slash commands.
- `.github/` contains the existing Copilot guidance.
- When project conventions change, keep both areas aligned so the assistants do not drift apart.
