---
mode: agent
description: "Generate or expand focused xUnit coverage for a parser, normalizer, or ingestion helper using the current repository patterns."
---

# New Parser / Service Test

Use the current repository guidance from:
- `docs/08_test_strategy.md`
- `src/backend/Jobs.Tests/Jobs.Tests.csproj`
- nearby tests under `src/backend/Jobs.Tests/Ingestion/`

## Parameters

- **Subject class**: ${input:subjectClass:PascalCase class name, e.g. GupyJobsJsonParser}
- **Fixture file**: ${input:fixtureFile:Fixture filename under src/backend/tests/fixtures/}
- **Fixture format**: ${input:fixtureFormat:json or html}

## Task

Generate or expand a compilable xUnit test file at:

`src/backend/Jobs.Tests/Ingestion/${subjectClass}Tests.cs`

Before writing the test:
1. Inspect the real implementation for `${subjectClass}` under `src/backend/Jobs.Infrastructure/`.
2. Inspect nearby tests and match the current local naming/assertion style.

## Requirements

- use xUnit only
- default namespace: `Jobs.Tests.Ingestion`
- class name: `${subjectClass}Tests`
- load the fixture from `Path.Combine("fixtures", "${fixtureFile}")` or `Path.Combine(AppContext.BaseDirectory, "fixtures", "${fixtureFile}")`, matching nearby tests
- instantiate the real implementation directly or call its real static API
- do not use Moq or a DI container
- cover at minimum:
  - happy-path parsing or mapping
  - empty or invalid input behavior
  - source URL / identifier mapping when relevant

## Constraints

- no real HTTP, database, or sleeps
- prefer specific assertions over `Assert.True(...)` when a stronger assertion exists
- do not claim pipeline/API/frontend coverage if the test only validates a parser or helper

## Final summary

At the end, summarize:
- which tests were added or updated
- which files changed
- what validation was run
