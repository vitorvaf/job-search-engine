---
mode: agent
description: "Scaffold a new IJobSource connector for a job board. Use when adding a new job source/crawler to the ingestion pipeline."
---

# New Job Source Connector

Scaffold a complete, working `IJobSource` connector for a new job board.

## Parameters

- **Source name**: ${input:sourceName:PascalCase name of the source, e.g. Greenhouse}
- **Source URL**: ${input:sourceUrl:Base URL of the job board, e.g. https://boards.greenhouse.io}
- **Response format**: ${input:responseFormat:json or html}

## What to generate

Generate ALL of the following — do not skip any step:

### 1. Connector class

File: `src/backend/Jobs.Infrastructure/Ingestion/${sourceName}JobSource.cs`

- Implement `IJobSource` exactly as defined in `src/backend/Jobs.Infrastructure/Ingestion/IJobSource.cs`.
- Use `IHttpClientFactory` (named client `"Sources"`) — never `new HttpClient()`.
- Read enabled/config from `AppOptions` via `IOptions<AppOptions>`.
- Return `IAsyncEnumerable<ParsedSourceJob>` — do NOT change the interface.
- Use `[EnumeratorCancellation] CancellationToken ct` as the last parameter.
- Log with `ILogger<${sourceName}JobSource>` — no `Console.WriteLine`.
- Respect `opts.DelayBetweenRequestsMs` between paginated requests.
- Do NOT handle transient HTTP errors manually — Polly is already configured.

```csharp
public sealed class ${sourceName}JobSource(
    IHttpClientFactory httpFactory,
    IOptions<AppOptions> options,
    ILogger<${sourceName}JobSource> logger) : IJobSource
{
    private readonly HttpClient _http = httpFactory.CreateClient("Sources");

    public string Name => "${sourceName}";
    public bool Enabled => options.Value.Sources.${sourceName}?.Enabled ?? false;

    public async IAsyncEnumerable<ParsedSourceJob> FetchJobsAsync(
        IngestionFetchOptions opts,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // TODO: implement pagination and parsing
        yield break;
    }
}
```

### 2. DI Registration

In `src/backend/Jobs.Infrastructure/DependencyInjection.cs`, add inside `AddJobsInfrastructure`:

```csharp
services.AddScoped<IJobSource, ${sourceName}JobSource>();
```

### 3. Options class (if the source needs configuration)

If the source needs specific config (API key, company slug, etc.), add a nested class or property to `src/backend/Jobs.Infrastructure/Options/AppOptions.cs`.

### 4. Test fixture

File: `src/backend/tests/fixtures/${sourceName:lower}_jobs.${responseFormat}`

Create a realistic, anonymised sample response from the source API (or a representative HTML page). Use publicly available job data — no real personal data.

### 5. Unit test

File: `src/backend/Jobs.Tests/Ingestion/${sourceName}JobSourceTests.cs`

- Test class: `${sourceName}JobSourceTests`
- Namespace: `Jobs.Tests.Ingestion`
- Cover at minimum:
  - `Parse_ValidFixture_ReturnsAtLeastOneJob`
  - `Parse_ValidFixture_MapsTitle`
  - `Parse_ValidFixture_MapsCompanyName`
- Load fixtures from `Path.Combine("fixtures", "${sourceName:lower}_jobs.${responseFormat}")`.
- No mocking — instantiate the parser directly.

### 6. Sample documentation

File: `docs/samples/sample_source_${sourceName:lower}.${responseFormat}`

Copy the same fixture content here (or a representative excerpt) for documentation purposes.

## Constraints (from PROJECT_RULES.md)

- Worker must remain idempotent — `ParsedSourceJob` mapping must be deterministic.
- `Fingerprint` is a singleton injected via DI — never compute SHA-256 inline.
- Use `System.Text.Json` — never `Newtonsoft.Json`.
- Schema changes require updating `Jobs.Infrastructure/Data/schema.sql` AND `JobPostingEntity` AND `MappingExtensions`.
