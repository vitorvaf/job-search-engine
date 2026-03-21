# GitHub Copilot — Repository Instructions

## Project Overview

**job-search-engine** is a job aggregator that crawls multiple public job boards (InfoJobs, Gupy, Workday, JsonLd, corporate career pages), normalises all postings into a single domain model, deduplicates them, stores them in PostgreSQL, indexes them in Meilisearch, and exposes them via a REST API consumed by a Next.js frontend.

```
[Sources] → Worker (fetch → parse → normalise → dedup) → PostgreSQL
                                                        → Meilisearch (search index)
Browser  → Next.js BFF Route Handlers → Jobs.Api REST → Meilisearch / PostgreSQL
```

---

## Repository Structure

```
src/backend/          .NET 8 solution (Jobs.sln)
  Jobs.Domain/        Pure domain models — no infrastructure dependencies
  Jobs.Infrastructure/ EF Core DbContext, IJobSource connectors, MeiliClient, IngestionPipeline
  Jobs.Api/           ASP.NET Core Minimal API (no controllers)
  Jobs.Worker/        BackgroundService that orchestrates ingestion
  Jobs.Tests/         xUnit unit tests; test fixtures live in src/backend/tests/fixtures/

src/frontend/         Next.js 15 / App Router
  app/                Pages and Route Handlers (BFF)
  components/         React Server / Client Components
  lib/                Shared TypeScript utilities, types, API proxy

docs/                 Authoritative specification (read before suggesting changes)
  PROJECT_RULES.md    Team constitution — constraints and definitions of done
  02_domain_model.md  Canonical JobPosting model
  03_architecture.md  Architecture decisions
  05_ranking_and_dedupe.md  Deduplication strategy
```

---

## Domain Model — Core Concepts

The canonical model is `Jobs.Domain.Models.JobPosting`. Key fields:
- `Id` (Guid), `Title`, `CompanyName`, `LocationText`
- `WorkMode` (enum: Unknown, OnSite, Remote, Hybrid)
- `Seniority` (enum: Unknown, Intern, Junior, Mid, Senior, Lead, Manager, Director, Executive)
- `EmploymentType` (enum: Unknown, FullTime, PartTime, Contract, Freelance, Internship, Temporary)
- `Tags` (string[]) — lowercase, normalised technology/skill tags
- `Fingerprint` — SHA-256 of (title + company + location) for deduplication
- `Status` (enum: Active, Removed)

**Never invent fields on `JobPosting` or the DB entities without updating migrations and the Meilisearch index definition.**

---

## Backend Conventions (.NET 8 / C#)

- Target `net8.0`; use `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` in all projects.
- Use **Minimal APIs** (no MVC controllers) in `Jobs.Api`. Route handlers stay in `Program.cs`.
- Use **`record`** for immutable DTOs and parsed data (`ParsedSourceJob`). Use **`class`** for EF entities and domain models.
- Prefer `IAsyncEnumerable<T>` for streaming, `CancellationToken ct` as the last parameter in every async method.
- All HTTP calls go through the named `"Sources"` `HttpClient` registered in `DependencyInjection.cs`. Never create raw `HttpClient` instances.
- Use `Polly` for retry/backoff policies already configured in `DependencyInjection.cs`. Do not add ad-hoc `try/catch` for transient HTTP errors.
- EF Core: use `JobsDbContext` via DI. Use `await db.Database.EnsureCreatedAsync()` at startup only — this project does not use EF Migrations; the schema is in `Jobs.Infrastructure/Data/schema.sql`.
- Configuration: bind strongly-typed options via `services.Configure<TOptions>(config.GetSection("..."))` — never read `IConfiguration` directly in business logic.
- `Fingerprint` is a singleton service (`Jobs.Infrastructure.Ingestion.Fingerprint`). Always inject it; never recompute the hash inline.

---

## Adding a New Job Source (IJobSource Pattern)

Every new job source **must** follow this checklist (from `docs/PROJECT_RULES.md`):

1. Create `src/backend/Jobs.Infrastructure/Ingestion/<SourceName>JobSource.cs` implementing `IJobSource`.
2. Register in `DependencyInjection.cs` via `services.AddScoped<IJobSource, <SourceName>JobSource>()`.
3. Add a sample response in `docs/samples/sample_source_<sourcename>.json` (or `.html`).
4. Add test fixtures in `src/backend/tests/fixtures/`.
5. Add at least one xUnit test in `src/backend/Jobs.Tests/Ingestion/<SourceName>Tests.cs` that covers happy-path parsing.
6. Respect rate limits: use `IngestionFetchOptions` with delay between requests.
7. All sources produce `IAsyncEnumerable<ParsedSourceJob>` — do not change the interface.

```csharp
// Minimal IJobSource implementation skeleton
public sealed class ExampleJobSource(
    IHttpClientFactory httpFactory,
    IOptions<AppOptions> options,
    ILogger<ExampleJobSource> logger) : IJobSource
{
    public string Name => "Example";
    public bool Enabled => options.Value.Sources.Example?.Enabled ?? false;

    public async IAsyncEnumerable<ParsedSourceJob> FetchJobsAsync(
        IngestionFetchOptions opts,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // implementation
        yield break;
    }
}
```

---

## Frontend Conventions (Next.js 15 / TypeScript)

- Use the **App Router** exclusively. No `pages/` directory.
- **BFF rule**: the browser never calls `Jobs.Api` directly. All data fetches go through the Next.js Route Handlers in `src/frontend/app/api/`.
- Route Handlers validate and sanitise query params before forwarding to the backend.
- Prefer **React Server Components** (RSC) for data-fetching pages. Use `"use client"` only when browser APIs (localStorage, event handlers) are needed.
- Styling: **Tailwind CSS utility classes only** — no inline styles, no CSS Modules.
- Icons: **Lucide React** — never add other icon libraries.
- Favourites persist in `localStorage` via `src/frontend/lib/storage.ts` — never hit the backend for favourites.
- Type definitions live in `src/frontend/lib/types.ts`. The `JobPosting` and `JobListItem` interfaces mirror the backend API contract from `docs/07_api_contracts.md`.

---

## Testing Conventions

- Backend unit tests use **xUnit** (no Moq — test against real implementations or minimal stubs).
- Test class naming: `<SubjectClass>Tests` in namespace `Jobs.Tests.Ingestion`.
- Test method naming: `<Method>_<Scenario>_<ExpectedResult>` (e.g., `Parse_ValidHtml_ReturnsExpectedJobs`).
- Test fixtures (HTML/JSON files) live in `src/backend/tests/fixtures/` and are embedded via `<Content>` in `Jobs.Tests.csproj`.
- Load fixtures with `File.ReadAllText(Path.Combine("fixtures", "<filename>"))`.
- No integration tests against live databases or external HTTP in the test suite — use `JsonFixtureJobSource` for realistic end-to-end tests within the Worker pipeline.

---

## What Copilot Should NOT Do

- Do **not** add EF Migrations — the schema is managed via `schema.sql`.
- Do **not** create MVC controllers, Razor Pages or `pages/` directory in the frontend.
- Do **not** add new NuGet packages without checking if the functionality is already available in the existing stack (Polly, EF Core, System.Text.Json).
- Do **not** use `Newtonsoft.Json` — the project uses `System.Text.Json` exclusively.
- Do **not** call the backend API directly from React client components — always go through the BFF Route Handlers.
- Do **not** add `Console.WriteLine` for logging — use `ILogger<T>` injected via DI.
- Do **not** store secrets in `appsettings.json` — use environment variables or `.env` (see `.env.example`).
- Do **not** modify the `JobPosting` domain model without updating `JobPostingEntity`, `MappingExtensions`, and the Meilisearch index configuration.

---

## Environment & Running Locally

```bash
# 1. Start infrastructure services
cp src/backend/.env.example src/backend/.env
docker compose up -d

# 2. Run the API
dotnet run --project src/backend/Jobs.Api

# 3. Run the Worker (continuous)
dotnet run --project src/backend/Jobs.Worker

# 4. Run the Worker (single source, once)
dotnet run --project src/backend/Jobs.Worker -- --run-once --source=InfoJobs

# 5. Run backend tests
dotnet test src/backend/Jobs.sln

# 6. Run the frontend
cd src/frontend && npm install && npm run dev
```

API: `http://localhost:5000` · Frontend: `http://localhost:3000` · Swagger: `http://localhost:5000/swagger`
