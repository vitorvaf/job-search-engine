---
description: Run backend validation with dotnet test and summarize failures or gaps.
agent: tester
subtask: true
---
Run `dotnet test src/backend/Jobs.sln` and summarize the result.

Focus area: `$ARGUMENTS`

Report:
1. command(s) run
2. pass/fail result
3. failing tests or errors
4. remaining validation gaps
