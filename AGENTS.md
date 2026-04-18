# OpenCode Guide

This file is the OpenCode entrypoint for `job-search-engine`.

It is intentionally thin. Reuse the repository guidance that already exists instead of duplicating it.

## Instruction Order

1. Trust the current implementation under `src/` for real behavior.
2. Use `CLAUDE.md` as the main operational map for the repository.
3. Use `docs/PROJECT_RULES.md` as the project constitution.
4. Use `.github/copilot-instructions.md` as a compact cross-stack reference when a change spans backend, worker, API, BFF, and frontend.
5. Pull only the relevant area guidance when needed:
   - backend: `.claude/rules/backend.md`, `.github/instructions/backend.instructions.md`
   - frontend/BFF: `.claude/rules/frontend.md`, `.github/instructions/frontend.instructions.md`
   - testing: `.claude/rules/testing.md`, `.github/instructions/testing.instructions.md`
6. Use `.claude/commands/` and `.github/prompts/` as workflow references, not as a second source of truth.

If docs and code disagree, prefer the current code in `src/`, then suggest a docs alignment follow-up.

## System Map

- Worker orchestration: `src/backend/Jobs.Worker/Worker.cs`
- Ingestion pipeline: `src/backend/Jobs.Infrastructure/Ingestion/IngestionPipeline.cs`
- Ingestion families and parsers: `src/backend/Jobs.Infrastructure/Ingestion/`
- Persistence and mapping: `src/backend/Jobs.Infrastructure/Data/`
- Search indexing: `src/backend/Jobs.Infrastructure/Search/MeiliClient.cs`
- API contract: `src/backend/Jobs.Api/Program.cs`
- BFF boundary: `src/frontend/app/api/` and `src/frontend/lib/api-proxy.ts`
- Frontend normalization: `src/frontend/lib/normalizers.ts` and `src/frontend/lib/types.ts`

## Non-Negotiable Rules

- `Jobs.Api` must remain a Minimal API in `Program.cs`.
- Do not add MVC controllers or Razor Pages.
- Frontend uses the Next.js App Router only.
- The browser must never call `Jobs.Api` directly; browser traffic goes through `src/frontend/app/api/`.
- Database schema changes go in `src/backend/Jobs.Infrastructure/Data/schema.sql`; do not add EF migrations.
- All outbound HTTP must use the named `"Sources"` `HttpClient`.
- `IJobSource` implementations emit `IAsyncEnumerable<JobPosting>` from `FetchAsync(...)`.
- Use the injected `Fingerprint` service for dedupe; do not recompute hashes ad hoc.
- Prefer existing ingestion families such as `CorporateCareers`, `JsonLd`, and `GupyCompanies` before creating a brand-new connector.
- New ingestion sources must ship with fixtures, xUnit coverage, and docs updates together.
- If filters, enums, sorting, pagination, or API contracts change, review backend API, BFF route handlers, `api-proxy.ts`, `normalizers.ts`, `types.ts`, `constants.ts`, and affected UI together.
- When that boundary changes, run `node scripts/check-boundary-drift.mjs`.

## Validation Defaults

- Backend changes: `dotnet test src/backend/Jobs.sln`
- Frontend or BFF changes: `node scripts/check-boundary-drift.mjs`
- Frontend UI or route-handler changes: `npm run lint` and `npm run build` in `src/frontend`
- Ingestion changes: relevant xUnit coverage and, when practical, `dotnet run --project src/backend/Jobs.Worker -- --run-once --source=<Name>`

## OpenCode Workflow

- Use `.opencode/agents/` for role-specific work such as planning, implementation, review, testing, debugging, ingestion analysis, and BFF analysis.
- Use `.opencode/commands/` as reusable entrypoints for common daily tasks.
- Keep changes minimal, validate the touched boundary, and avoid broad exploratory edits.
