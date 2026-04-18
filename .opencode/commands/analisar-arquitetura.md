---
description: Map the architecture, flow, and impact area for a feature or subsystem.
agent: planner
subtask: true
---
Use `@AGENTS.md`, `@CLAUDE.md`, `@docs/03_architecture.md`, `@docs/04_ingestion_sources.md`, and `@docs/07_api_contracts.md` as entrypoints, then inspect the current implementation under `src/`.

Analyze the architecture for: `$ARGUMENTS`

Return:
1. End-to-end flow through the system
2. Main files and responsibilities
3. Invariants and architectural constraints
4. Most likely impact zones for a change

Prefer the current code under `src/` when docs diverge. Do not edit files.
