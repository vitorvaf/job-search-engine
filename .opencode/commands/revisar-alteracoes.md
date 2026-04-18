---
description: Review the current changes for bugs, regressions, drift, and missing tests.
agent: reviewer
subtask: true
---
Current worktree status:
!`git status --short`

Current diff summary:
!`git diff --stat`

Review the current changes in this repository. Additional focus: `$ARGUMENTS`

Requirements:
- findings first, ordered by severity, with file references
- prioritize contract drift, ingestion regressions, missing tests, and validation gaps
- if there are no findings, say so explicitly
