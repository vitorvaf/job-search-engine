---
paths:
  - "src/backend/Jobs.Tests/**/*"
  - "src/backend/tests/fixtures/**/*"
---

# Testing rules (xUnit)

- Use xUnit for backend tests.
- Follow the existing `Jobs.Tests.Ingestion` namespace pattern unless nearby files already establish a different test namespace.
- Name test classes `<Subject>Tests`.
- Follow the nearby naming style for test methods. The repository currently mixes `Should...` tests and `<Subject>_<Scenario>_<ExpectedResult>` tests.
- Keep tests in Arrange / Act / Assert format with clear blank-line separation.
- Store HTML/JSON fixtures in `src/backend/tests/fixtures/`; do not inline large payloads in the test file.
- Load fixtures with `Path.Combine("fixtures", "...")` or `Path.Combine(AppContext.BaseDirectory, "fixtures", "...")`, matching the surrounding style.
- Prefer specific assertions over `Assert.True(...)` when a stronger assertion is available.
- Do not make real HTTP, database, or external service calls from the test suite.
- Do not add Moq or other mocking libraries; use real implementations or small manual stubs only when unavoidable.
- The current suite is strongest around parser, normalizer, fingerprint, and fixture-based validation coverage. Add tests in those layers first when expanding coverage.
- New parsers and sources should cover the happy path plus empty or malformed input when practical.
- Captured real fixtures should live alongside the existing naming patterns such as `*_real.json` and `*_nextdata.json`.
