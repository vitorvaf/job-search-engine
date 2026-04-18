---
description: Implement a feature safely and incrementally using repository conventions.
agent: implementer
subtask: true
---
Use `@AGENTS.md`, `@CLAUDE.md`, and the current implementation under `src/`.

Implement this feature: `$ARGUMENTS`

Requirements:
- inspect the real code paths before editing
- prefer the smallest correct change
- reuse existing patterns and source families
- preserve Minimal API, App Router, and the internal BFF boundary
- if the boundary changes, run `node scripts/check-boundary-drift.mjs`
- if frontend code changes, validate with `npm run lint` and `npm run build` in `src/frontend`
- if backend or ingestion changes, run the relevant `dotnet test` coverage

At the end, summarize changed files and validations run.
