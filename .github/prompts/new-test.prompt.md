---
mode: agent
description: "Scaffold a complete xUnit test class for a parser or normalizer. Use when adding tests for an IJobSource parser (HTML or JSON), a text normalizer, or a fingerprint computation."
---

# New Parser / Normalizer Test

Scaffold a complete, compilable xUnit test class for an existing parser or service.

## Parameters

- **Subject class**: ${input:subjectClass:PascalCase name of the class under test, e.g. GupyJobsJsonParser}
- **Fixture file**: ${input:fixtureFile:Filename of the fixture in src/backend/tests/fixtures/, e.g. gupy_company_jobs.json}
- **Fixture format**: ${input:fixtureFormat:json or html}

## What to generate

Generate a complete, compilable test file at:

`src/backend/Jobs.Tests/Ingestion/${subjectClass}Tests.cs`

### Requirements

1. **Namespace**: `Jobs.Tests.Ingestion`
2. **Class name**: `${subjectClass}Tests`
3. **Load fixture** using:
   ```csharp
   var raw = File.ReadAllText(Path.Combine("fixtures", "${fixtureFile}"));
   ```
4. **Instantiate** `${subjectClass}` directly — no mocking, no DI container.
5. **Cover at minimum** these test methods (use `[Fact]` for each):
   - `Parse_ValidFixture_ReturnsAtLeastOneJob` — assert `Assert.NotEmpty(...)`.
   - `Parse_ValidFixture_MapsTitle` — assert the first result has a non-empty `Title`.
   - `Parse_ValidFixture_MapsCompanyName` — assert `CompanyName` is non-empty.
   - `Parse_ValidFixture_MapsSourceUrl` — assert `SourceUrl` is a valid URI (use `Uri.TryCreate`).
   - `Parse_EmptyInput_ReturnsEmpty` — pass `""` or `"[]"` — assert empty result, no exception.
6. If the parser deals with work mode, seniority, or tags, add:
   - `Parse_ValidFixture_WorkModeIsKnown` — assert `WorkMode != WorkMode.Unknown` for at least one job.
   - `Parse_ValidFixture_TagsAreLowercase` — assert all tags are lowercase.
7. **Method naming**: `<Method>_<Scenario>_<ExpectedResult>`.
8. **Pattern**: Arrange / Act / Assert with blank-line separation.
9. **No** `Thread.Sleep`, `Task.Delay`, real HTTP calls, or database access.
10. **No** Moq or any mocking library — instantiate real objects only.

### Template structure

```csharp
using Jobs.Infrastructure.Ingestion;

namespace Jobs.Tests.Ingestion;

public sealed class ${subjectClass}Tests
{
    private static readonly string FixturePath = Path.Combine("fixtures", "${fixtureFile}");

    [Fact]
    public void Parse_ValidFixture_ReturnsAtLeastOneJob()
    {
        // Arrange
        var raw = File.ReadAllText(FixturePath);
        var parser = new ${subjectClass}();

        // Act
        var jobs = parser.Parse(raw).ToList();

        // Assert
        Assert.NotEmpty(jobs);
    }

    [Fact]
    public void Parse_ValidFixture_MapsTitle()
    {
        // Arrange
        var raw = File.ReadAllText(FixturePath);
        var parser = new ${subjectClass}();

        // Act
        var jobs = parser.Parse(raw).ToList();

        // Assert
        Assert.All(jobs, j => Assert.False(string.IsNullOrWhiteSpace(j.Title)));
    }

    // ... additional tests
}
```

> Adapt the constructor call (`new ${subjectClass}()`) if the class requires constructor arguments — check the actual signature in `src/backend/Jobs.Infrastructure/Ingestion/${subjectClass}.cs` before generating.

## Constraints

- Use `System.Text.Json` if you need to deserialise in a helper — never `Newtonsoft.Json`.
- Do NOT add `using Moq;` or any mocking imports.
- The test file must compile without referencing `Jobs.Api` or `Jobs.Worker` — only `Jobs.Infrastructure` and `Jobs.Domain`.
