---
argument-hint: <SourceName|JobId|flow>
description: Trace a job or source through ingestion, persistence, indexing, API, BFF, and UI.
---

Use the current repository guidance from @CLAUDE.md, @docs/03_architecture.md, @docs/04_ingestion_sources.md, @docs/07_api_contracts.md, @src/backend/Jobs.Worker/Worker.cs, @src/backend/Jobs.Infrastructure/Ingestion/IngestionPipeline.cs, @src/backend/Jobs.Infrastructure/Data/Mapping/MappingExtensions.cs, @src/backend/Jobs.Infrastructure/Search/MeiliClient.cs, @src/backend/Jobs.Api/Program.cs, @src/frontend/app/api/jobs/route.ts, @src/frontend/app/api/jobs/[id]/route.ts, @src/frontend/lib/normalizers.ts, and @src/frontend/lib/types.ts.

Task:

- Trace `$1` through the system from source fetch/parsing to API/BFF/UI consumption.
- Prefer the real code path over older docs or prompts when they disagree.
- If the target looks ingestion-heavy, use the `parser-reviewer` subagent when helpful.
- If the trace crosses API/BFF/frontend boundaries, use the `boundary-reviewer` subagent when helpful.

At the end, summarize:

- the file path of the flow
- where normalization, dedupe, persistence, and indexing happen
- which tests or validations are most relevant
- where the biggest ambiguity or regression risk is
