---
argument-hint: <SubjectClass> <FixtureFile> <json|html>
description: Scaffold or expand a focused xUnit test class for an ingestion parser or service.
---

Use the current repository guidance from @CLAUDE.md, @.claude/rules/testing.md, @docs/08_test_strategy.md, and @src/backend/Jobs.Tests/Jobs.Tests.csproj.

Task:

- Subject class: `$1`
- Fixture file: `$2`
- Fixture format: `$3`

If any required argument is missing, stop and tell me to run:

`/new-test <SubjectClass> <FixtureFile> <json|html>`

Before generating tests, inspect the real implementation for `$1` under `src/backend/Jobs.Infrastructure/` and adapt to the actual API shape used by the codebase.

Requirements:

- use xUnit only
- default namespace: `Jobs.Tests.Ingestion`
- class name: `$1Tests`
- load the fixture from `Path.Combine("fixtures", "$2")` or `Path.Combine(AppContext.BaseDirectory, "fixtures", "$2")`, matching nearby test style
- instantiate the real implementation directly or call its real static API
- follow the naming and assertion style already used by nearby tests for the same area
- do not use Moq or a DI container
- cover at minimum:
  - happy-path parsing or mapping
  - empty or invalid input behavior
  - source URL / identifier mapping when relevant
- keep Arrange / Act / Assert formatting
- avoid real HTTP, database, and sleeps

At the end, summarize:

- which tests were added or updated
- which files changed
- what validation you ran
