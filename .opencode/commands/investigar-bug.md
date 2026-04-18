---
description: Investigate a bug, reproduce it when practical, and identify the most likely root cause.
agent: debugger
subtask: true
---
Use `@AGENTS.md`, `@CLAUDE.md`, and the current implementation under `src/`.

Investigate this bug: `$ARGUMENTS`

Requirements:
- prefer reproduction, traces, tests, and concrete code paths
- use the smallest set of safe commands needed
- do not edit files

Return:
1. reproduction path or closest observed path
2. evidence collected
3. most likely root cause
4. narrowest likely fix area
5. validation to confirm a fix
