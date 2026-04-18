---
description: Review the Next.js BFF boundary, route handlers, and frontend normalization path.
agent: frontend-bff-specialist
subtask: true
---
Use `@AGENTS.md`, `@CLAUDE.md`, `@docs/07_api_contracts.md`, `@src/backend/Jobs.Api/Program.cs`, `src/frontend/app/api/`, `@src/frontend/lib/api-proxy.ts`, `@src/frontend/lib/normalizers.ts`, `@src/frontend/lib/types.ts`, and `@src/frontend/lib/constants.ts`.

Validate this BFF or frontend-boundary change: `$ARGUMENTS`

Requirements:
- check forwarding, sanitization, normalization, and contract drift
- ensure the browser does not call `Jobs.Api` directly
- include `node scripts/check-boundary-drift.mjs` in the recommended validation
- do not edit files
