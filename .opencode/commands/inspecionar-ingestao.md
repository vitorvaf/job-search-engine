---
description: Inspect ingestion connectors, parsers, dedupe, fixtures, and worker pipeline impact.
agent: ingestion-specialist
subtask: true
---
Use `@AGENTS.md`, `@CLAUDE.md`, `@docs/04_ingestion_sources.md`, `@docs/05_ranking_and_dedupe.md`, and the current ingestion code under `src/backend/Jobs.Infrastructure/Ingestion/`.

Inspect this ingestion area: `$ARGUMENTS`

Requirements:
- check whether an existing source family should be reused
- check parser normalization and dedupe implications
- check fixture, xUnit, worker config, and docs alignment needs
- do not edit files

Return findings first, then required file set, then minimum validation.
