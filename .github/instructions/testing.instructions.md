---
applyTo: "src/backend/Jobs.Tests/**,src/backend/tests/fixtures/**"
---

# Testing Instructions (xUnit / .NET 8)

## Current test strategy

- Backend tests use xUnit.
- The repository currently has the strongest automated coverage around:
  - parsers
  - text normalization
  - dedupe fingerprinting
  - fixture-based validation tests with captured source payloads
- Fixtures live in `src/backend/tests/fixtures/` and are copied by `Jobs.Tests.csproj`.
- There is no established suite yet for API, worker, pipeline, BFF, or frontend unit/e2e tests.

## Rules

- Use xUnit only.
- Do not add Moq or another mocking framework unless there is no simpler alternative.
- Follow the nearby test naming style; the repo mixes `Should...` and `<Subject>_<Scenario>_<ExpectedResult>`.
- Use Arrange / Act / Assert formatting.
- Prefer specific assertions over broad `Assert.True(...)` checks when a stronger assertion exists.
- Load fixtures from `Path.Combine("fixtures", "...")` or `Path.Combine(AppContext.BaseDirectory, "fixtures", "...")`, matching surrounding tests.
- Do not make real HTTP, database, or external service calls in the test suite.

## Source and parser changes

When changing a parser or ingestion source:
- add or update a fixture under `src/backend/tests/fixtures/`
- add or expand tests under `src/backend/Jobs.Tests/Ingestion/`
- if you have a captured real response, consider extending `IngestionSourceValidationTests`
