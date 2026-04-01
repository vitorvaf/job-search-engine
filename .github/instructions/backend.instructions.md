---
applyTo: "src/backend/**"
---

# Backend Coding Instructions (.NET 8 / C#)

## Project layout

```text
Jobs.Domain/         Domain model and enums
Jobs.Infrastructure/ EF Core, ingestion sources, parsers, MeiliClient, IngestionPipeline
Jobs.Api/            ASP.NET Core Minimal API; routes live in Program.cs
Jobs.Worker/         BackgroundService orchestrating ingestion
Jobs.Tests/          xUnit tests; fixtures live in src/backend/tests/fixtures/
```

## Rules

- Target `net8.0`.
- Use `System.Text.Json` only.
- Register infrastructure services in `Jobs.Infrastructure/DependencyInjection.cs`.
- Use strongly typed options in `Jobs.Infrastructure/Options/`.
- Use `ILogger<T>`; do not use `Console.WriteLine`.
- All outbound HTTP must use the named `"Sources"` `HttpClient`.
- Reuse the resilience policies already configured in `DependencyInjection.cs`.
- `IJobSource` returns `IAsyncEnumerable<JobPosting>` from `FetchAsync(IngestionFetchOptions options, CancellationToken ct)`.
- `ParsedSourceJob` is only an intermediate parser shape.
- Build `JobPosting` with its nested value objects (`JobSourceRef`, `CompanyRef`, `LocationRef`, `SalaryRange`, `DedupeInfo`).
- Use the injected `Fingerprint` service for dedupe data.
- `Jobs.Api` uses Minimal API in `Program.cs`; do not add MVC controllers or Razor Pages.
- Schema changes go in `Jobs.Infrastructure/Data/schema.sql`; do not add EF migrations.

## New source decision tree

Before creating a connector:

1. Check whether the source fits an existing family:
   - `CorporateCareers`
   - `JsonLd`
   - `GupyCompanies`
   - existing dedicated parsers such as Workday or InfoJobs
2. Only create a new `IJobSource` when the fetch/parse behavior is materially different.

If configuration changes are required:
- update `AppOptions`
- keep `appsettings.json` and `appsettings.Development.json` aligned in both `Jobs.Api` and `Jobs.Worker`

## Boundary review triggers

When changing fields used by search, filters, or API payloads, review these together:
- `Jobs.Domain/Models/`
- `Jobs.Infrastructure/Data/Entities/`
- `Jobs.Infrastructure/Data/Mapping/MappingExtensions.cs`
- `Jobs.Infrastructure/Search/MeiliClient.cs`
- `Jobs.Api/Program.cs`
