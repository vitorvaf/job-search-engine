# Contributing to job-search-engine

Thank you for your interest in contributing. This guide explains how to run the project locally, which conventions matter, and how to submit focused changes without breaking the ingestion and search flow.

## Prerequisites

| Tool | Version | Install |
|------|---------|---------|
| .NET SDK | 8.0+ | https://dotnet.microsoft.com/download |
| Node.js | 20+ | https://nodejs.org |
| Docker + Docker Compose | latest | https://docs.docker.com/get-docker/ |

## Local setup

```bash
# 1. Clone the repository
git clone https://github.com/vitorvaf/job-search-engine.git
cd job-search-engine

# 2. Start infrastructure services
cp .env.example .env
docker compose up -d

# 3. Run the API
dotnet run --project src/backend/Jobs.Api

# 4. Run the Worker
dotnet run --project src/backend/Jobs.Worker -- --run-once --source=InfoJobs

# 5. Run the frontend
cp src/frontend/.env.local.example src/frontend/.env.local
cd src/frontend
npm install
npm run dev
```

Default local URLs:
- API: `http://localhost:5004`
- Swagger: `http://localhost:5004/swagger`
- Frontend: `http://localhost:3000`

Notes:
- The root `.env` is only for local `docker compose` overrides; the compose file already has sane defaults.
- The backend runs locally from `appsettings.json` / `appsettings.Development.json`; no backend `.env` file is required by default.
- `BACKEND_URL=http://localhost:5004` is only needed by the Next.js BFF.

## Project structure

```text
src/backend/
  Jobs.Domain/         Domain models and enums
  Jobs.Infrastructure/ EF Core, ingestion sources, parsers, MeiliClient, IngestionPipeline
  Jobs.Api/            ASP.NET Core Minimal API; routes live in Program.cs
  Jobs.Worker/         BackgroundService that runs ingestion sources
  Jobs.Tests/          xUnit test project
  tests/fixtures/      HTML/JSON fixtures copied into test output

src/frontend/
  app/                 Next.js App Router pages and BFF route handlers
  components/          UI components
  lib/
    api-proxy.ts       Query sanitization and backend fetch plumbing for the BFF
    normalizers.ts     Raw backend payload -> normalized frontend model
    types.ts           Frontend-facing models plus documented raw API shapes
    constants.ts       Filter options and defaults

docs/                  Architecture, contracts, ingestion guidance, and test strategy
```

## Development workflow

```bash
# Create a branch
git checkout -b feat/my-feature

# Run validation
dotnet test src/backend/Jobs.sln
cd src/frontend && npm run lint && npm run build

# Commit using Conventional Commits
git commit -m "feat(ingestion): add Greenhouse source"
```

### Commit message format

```text
<type>(<scope>): <short description>

Types: feat | fix | chore | docs | refactor | test | ci | perf
Scopes: ingestion | api | worker | frontend | infra | domain | tests
```

## Conventions

### Backend (.NET 8 / C#)

- Minimal API only. Do not add MVC controllers or Razor Pages.
- Use `System.Text.Json` only.
- Use the named `"Sources"` `HttpClient`; never instantiate `HttpClient` directly.
- Use `ILogger<T>` for logging; no `Console.WriteLine`.
- Keep infra wiring in `src/backend/Jobs.Infrastructure/DependencyInjection.cs`.
- `IJobSource` emits `IAsyncEnumerable<JobPosting>` from `FetchAsync(...)`.
- `ParsedSourceJob` is a parser/helper stage, not the final ingestion contract.
- Schema changes go through `src/backend/Jobs.Infrastructure/Data/schema.sql`; do not add EF migrations.

### Frontend (Next.js / TypeScript)

- App Router only.
- Browser traffic never calls `Jobs.Api` directly; use the BFF route handlers in `src/frontend/app/api/`.
- `src/frontend/lib/types.ts` contains both documented raw API shapes and the normalized frontend model used by the UI.
- `src/frontend/lib/normalizers.ts` is the place to adapt backend payloads for the UI.
- Changes to filters, sort, pagination, or enum values must review backend API, BFF route handlers, `api-proxy.ts`, `normalizers.ts`, `types.ts`, `constants.ts`, and the affected components together.

## Adding a new job source

Start with `docs/04_ingestion_sources.md` and `docs/PROJECT_RULES.md`.

Decision order:

1. Check whether the source fits an existing family:
   - `CorporateCareers`
   - `JsonLd`
   - `GupyCompanies`
   - existing dedicated parsers such as Workday or InfoJobs
2. Only create a brand-new `IJobSource` when the fetch/parse behavior is materially different.

Expected change set:

1. Update ingestion code or parser helpers under `src/backend/Jobs.Infrastructure/Ingestion/`.
2. Update `AppOptions` and `appsettings*.json` in both `Jobs.Api` and `Jobs.Worker` when configuration is required.
3. Add fixture data under `src/backend/tests/fixtures/`.
4. Add or expand xUnit coverage under `src/backend/Jobs.Tests/Ingestion/`.
5. Update docs or samples under `docs/` when they help explain the new pattern.
6. Validate with `dotnet run --project src/backend/Jobs.Worker -- --run-once --source=<Name>`.

Use the assistant prompts or slash commands only as scaffolding help. The current codebase patterns take precedence over older boilerplate.

## Running tests

```bash
# Backend
dotnet test src/backend/Jobs.sln

# Cross-stack boundary drift
node scripts/check-boundary-drift.mjs

# Frontend
cd src/frontend
npm run lint
npm run build
```

See `docs/08_test_strategy.md` for where coverage exists today and which layers still need expansion.

## Submitting a pull request

1. Keep the PR focused.
2. Run the relevant validation locally.
3. Fill in the PR template honestly.
4. Link the related issue when applicable.
5. Wait for CI before merge.

## Questions

Open a [Discussion](../../discussions) or follow the support guidance in [SUPPORT.md](SUPPORT.md).
