---
applyTo: "src/backend/Jobs.Tests/**"
---

# Testing Instructions (xUnit / .NET 8)

These instructions apply to all files under `src/backend/Jobs.Tests/`. Follow them when generating, modifying, or reviewing test code.

## Framework & Tooling

- Test framework: **xUnit v2** — no MSTest, NUnit, or FluentAssertions.
- Coverage: **coverlet** (`<PackageReference Include="coverlet.collector" ...>`) — already in `Jobs.Tests.csproj`.
- No Moq or other mocking frameworks — test against real implementations or minimal manual stubs.

## Test Class Naming

```
<SubjectClass>Tests
```

Namespace: `Jobs.Tests.Ingestion` (even for non-ingestion tests, keep the pattern consistent with existing tests).

Examples: `InfoJobsHtmlParserTests`, `FingerprintTests`, `JobTextNormalizerTests`.

## Test Method Naming

```
<MethodUnderTest>_<Scenario>_<ExpectedResult>
```

Examples:
- `Parse_ValidHtml_ReturnsExpectedJobs`
- `Compute_SameInputTwice_ReturnsSameFingerprint`
- `Normalise_NullTitle_ReturnsEmpty`

## Fixtures

- All HTML/JSON test data lives in `src/backend/tests/fixtures/`.
- Fixtures are declared as `<Content>` in `Jobs.Tests.csproj` and copied to output via `CopyToOutputDirectory = PreserveNewest`.
- Load fixtures in tests with:

```csharp
var html = File.ReadAllText(Path.Combine("fixtures", "infojobs_list.html"));
```

- **Never** hardcode large HTML/JSON strings inline — always use a fixture file.
- Fixture file naming: `<source>_<variant>.html|json` (e.g., `gupy_company_jobs.json`, `infojobs_detail.html`).

## Test Structure

Use the **Arrange / Act / Assert** pattern, with clear blank-line separation:

```csharp
[Fact]
public void Parse_ValidHtml_ReturnsExpectedTitle()
{
    // Arrange
    var html = File.ReadAllText(Path.Combine("fixtures", "infojobs_list.html"));
    var parser = new InfoJobsHtmlParser();

    // Act
    var jobs = parser.Parse(html).ToList();

    // Assert
    Assert.NotEmpty(jobs);
    Assert.Equal("Desenvolvedor .NET", jobs[0].Title);
}
```

- **`[Fact]`** for single scenario tests.
- **`[Theory]` + `[InlineData]`** for parameterised tests.
- No `[ClassFixture]` unless shared, expensive setup is truly necessary.

## Async Tests

```csharp
[Fact]
public async Task FetchJobsAsync_ValidFixture_YieldsAtLeastOneJob()
{
    // Arrange
    var source = new JsonFixtureJobSource(...);

    // Act
    var jobs = await source.FetchJobsAsync(new IngestionFetchOptions(), CancellationToken.None)
                           .ToListAsync();

    // Assert
    Assert.NotEmpty(jobs);
}
```

Always pass `CancellationToken.None` in tests — do NOT create real timeouts.

## Scope Rules

- **Unit tests only** — no integration tests against live PostgreSQL, Meilisearch, or external HTTP.
- Use `JsonFixtureJobSource` to test the full ingestion pipeline without live infrastructure.
- If you need a DB, use an in-memory or SQLite stub — never `docker-compose` or real Postgres.

## Coverage Expectations (per `docs/PROJECT_RULES.md`)

Every new `IJobSource` connector must have at minimum:
1. A fixture file representing a real (anonymised) API/HTML response.
2. A `Parse_ValidFixture_ReturnsExpectedJobs` test covering the happy path.
3. Optional: edge-case tests for empty results, missing fields, malformed input.

## What NOT to Write

- Do NOT test EF Core internals (query generation, migrations) — those are framework responsibilities.
- Do NOT write tests that make real HTTP calls — use fixtures.
- Do NOT add `Thread.Sleep` or `Task.Delay` in tests — tests must be deterministic and fast.
- Do NOT use `Assert.True(condition)` when a more specific assertion exists (`Assert.Equal`, `Assert.Contains`, `Assert.NotNull`, etc.).
