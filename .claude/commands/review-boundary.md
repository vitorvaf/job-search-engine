---
argument-hint: <change-summary>
description: Review backend/API/BFF/frontend contract impact for filters, enums, sorting, pagination, and payload normalization.
---

Use the current repository guidance from @CLAUDE.md, @docs/07_api_contracts.md, @scripts/check-boundary-drift.mjs, @src/backend/Jobs.Api/Program.cs, @src/frontend/app/api/jobs/route.ts, @src/frontend/app/api/jobs/[id]/route.ts, @src/frontend/app/api/sources/route.ts, @src/frontend/lib/api-proxy.ts, @src/frontend/lib/normalizers.ts, @src/frontend/lib/types.ts, @src/frontend/lib/constants.ts, and @.github/workflows/ci.yml.

Task:

- Review the boundary impact of `$1`.
- Use the `boundary-reviewer` subagent when a focused review helps.
- Enumerate the files that must stay aligned.
- Call out drift risks around filters, enums, sorts, pagination, ports, env vars, or BFF normalization.
- If code changed, recommend the minimum validation set, including `node scripts/check-boundary-drift.mjs`.

Return:

- findings first
- then required file set
- then validation checklist
