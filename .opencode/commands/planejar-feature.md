---
description: Produce an implementation plan for a feature with impacted files, risks, and validation.
agent: planner
subtask: true
---
Use `@AGENTS.md`, `@CLAUDE.md`, `@docs/PROJECT_RULES.md`, and the current code under `src/`.

Plan this feature: `$ARGUMENTS`

Requirements for the answer:
- map the touched flow first
- list impacted files by backend, worker/ingestion, API, BFF, frontend, tests, and docs when applicable
- call out boundary review needs for filters, enums, sorting, pagination, or contracts
- call out ingestion family reuse needs before proposing a new connector
- include the minimum validation matrix

Do not edit files.
