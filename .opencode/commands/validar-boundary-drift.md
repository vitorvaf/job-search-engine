---
description: Validate backend/BFF/frontend boundary consistency with the shared drift script.
agent: tester
subtask: true
---
Run `node scripts/check-boundary-drift.mjs` and assess whether the current changes create drift across the backend API, BFF route handlers, normalizers, frontend types, constants, or UI.

Additional focus: `$ARGUMENTS`

Return:
1. command result
2. boundary files likely involved
3. drift risks or confirmation that no drift was found
