# Contributing to job-search-engine

Thank you for your interest in contributing! This guide explains how to set up the project locally, the conventions to follow, and how to submit contributions.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Setup](#local-setup)
3. [Project Structure](#project-structure)
4. [Development Workflow](#development-workflow)
5. [Conventions](#conventions)
6. [Adding a New Job Source](#adding-a-new-job-source)
7. [Running Tests](#running-tests)
8. [Submitting a Pull Request](#submitting-a-pull-request)

---

## Prerequisites

| Tool | Version | Install |
|------|---------|---------|
| .NET SDK | 8.0+ | https://dotnet.microsoft.com/download |
| Node.js | 20+ | https://nodejs.org |
| Docker + Docker Compose | latest | https://docs.docker.com/get-docker/ |

---

## Local Setup

```bash
# 1. Clone the repository
git clone https://github.com/vitorvaf/job-search-engine.git
cd job-search-engine

# 2. Start infrastructure (PostgreSQL, Meilisearch, Redis)
cp src/backend/.env.example src/backend/.env
docker compose up -d

# 3. Run the API (http://localhost:5000, Swagger: http://localhost:5000/swagger)
dotnet run --project src/backend/Jobs.Api

# 4. Run the Worker (single pass — useful for local testing)
dotnet run --project src/backend/Jobs.Worker -- --run-once --source=InfoJobs

# 5. Run the frontend (http://localhost:3000)
cd src/frontend
cp .env.local.example .env.local   # or create manually: BACKEND_URL=http://localhost:5000
npm install
npm run dev
```

---

## Project Structure

```
src/backend/
  Jobs.Domain/         Domain models (JobPosting, enums) — no external dependencies
  Jobs.Infrastructure/ EF Core, IJobSource connectors, MeiliClient, IngestionPipeline
  Jobs.Api/            Minimal API (no MVC controllers) — route handlers in Program.cs
  Jobs.Worker/         BackgroundService orchestrating ingestion
  Jobs.Tests/          xUnit unit tests
  tests/fixtures/      HTML/JSON fixtures for parser tests

src/frontend/
  app/                 Next.js 15 App Router pages + BFF Route Handlers
  components/          React components (Server Components by default)
  lib/                 Utilities, types (mirror API contract), storage helpers

docs/                  Authoritative specification — read before changing domain model
```

---

## Development Workflow

```bash
# Create a branch
git checkout -b feat/my-feature

# Develop, then run tests
dotnet test src/backend/Jobs.sln
cd src/frontend && npm run lint && npm run build

# Commit using Conventional Commits
git commit -m "feat(ingestion): add Greenhouse connector"

# Push and open a PR — the PR template guides the checklist
git push -u origin feat/my-feature
```

### Commit message format (Conventional Commits)

```
<type>(<scope>): <short description>

Types: feat | fix | chore | docs | refactor | test | ci | perf
Scopes: ingestion | api | worker | frontend | infra | domain | tests
```

Examples:
- `feat(ingestion): add GreenhouseJobSource connector`
- `fix(api): clamp pageSize to max 100`
- `test(ingestion): add InfoJobsHtmlParser edge-case fixtures`
- `chore(ci): add frontend lint step to CI workflow`

---

## Conventions

### Backend (C# / .NET 8)

- **Minimal API only** — no MVC controllers or Razor Pages.
- **`record`** for DTOs (`ParsedSourceJob`); **`class`** for EF entities and domain models.
- **`System.Text.Json`** exclusively — never import `Newtonsoft.Json`.
- HTTP via named `"Sources"` `HttpClient` from `IHttpClientFactory` — never `new HttpClient()`.
- Retry/backoff via Polly (already configured) — no ad-hoc `try/catch` for transient errors.
- Logging via `ILogger<T>` — no `Console.WriteLine`.
- No EF Migrations — schema changes go in `Jobs.Infrastructure/Data/schema.sql`.
- Read `docs/02_domain_model.md` before touching `JobPosting` or its entity.

### Frontend (TypeScript / Next.js 15)

- **App Router** exclusively — no `pages/` directory.
- **BFF rule**: browser never calls `Jobs.Api` directly; all calls go through `app/api/` Route Handlers.
- **React Server Components** by default; add `"use client"` only for browser APIs.
- **Tailwind CSS** utility classes — no inline styles, no CSS Modules.
- **Lucide React** for icons — no other icon libraries.
- Type definitions in `lib/types.ts`, mirroring `docs/07_api_contracts.md`.

---

## Adding a New Job Source

Follow the checklist defined in `docs/PROJECT_RULES.md` and `docs/04_ingestion_sources.md`:

1. Create `src/backend/Jobs.Infrastructure/Ingestion/<Name>JobSource.cs` implementing `IJobSource`.
2. Register in `DependencyInjection.cs`.
3. Add fixture in `src/backend/tests/fixtures/`.
4. Add sample payload in `docs/samples/`.
5. Write at least one xUnit test in `Jobs.Tests/Ingestion/<Name>JobSourceTests.cs`.
6. Respect rate limits via `IngestionFetchOptions.DelayBetweenRequestsMs`.

Use the VS Code Copilot prompt `/new-source` to scaffold the boilerplate automatically.

---

## Running Tests

```bash
# All backend tests
dotnet test src/backend/Jobs.sln

# Verbose output with coverage
dotnet test src/backend/Jobs.sln --collect:"XPlat Code Coverage" --verbosity normal

# Frontend lint + build check
cd src/frontend && npm run lint && npm run build
```

---

## Submitting a Pull Request

1. Ensure all tests pass locally.
2. Fill in the PR template checklist completely.
3. Keep PRs focused — one feature or fix per PR.
4. Link the related issue (e.g., `Closes #42`).
5. A passing CI run is required before merge.

---

## Questions?

Open a [Discussion](../../discussions) or a [SUPPORT.md](SUPPORT.md) ticket.
