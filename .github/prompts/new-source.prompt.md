---
mode: agent
description: "Add or wire a new ingestion source using the current source families and repository conventions."
---

# New Job Source

Use the current repository guidance from:
- `docs/PROJECT_RULES.md`
- `docs/04_ingestion_sources.md`
- `src/backend/Jobs.Infrastructure/Ingestion/IJobSource.cs`
- `src/backend/Jobs.Infrastructure/Ingestion/IngestionPipeline.cs`
- `src/backend/Jobs.Infrastructure/DependencyInjection.cs`
- `src/backend/Jobs.Infrastructure/Options/AppOptions.cs`
- existing source families under `src/backend/Jobs.Infrastructure/Ingestion/`

## Parameters

- **Source name**: ${input:sourceName:Human-readable name, e.g. Greenhouse}
- **Source URL**: ${input:sourceUrl:Base URL or start URL}
- **Response format**: ${input:responseFormat:json or html}

## Task

Add the new source using the current repository patterns.

Before writing code:
1. Inspect whether the source fits an existing family such as `CorporateCareers`, `JsonLd`, or `GupyCompanies`.
2. Only create a new `IJobSource` when the fetch/parse behavior is materially different.

Implement the full change set needed for the chosen pattern:
- ingestion code and parser changes
- `AppOptions` and `appsettings*.json` updates in both `Jobs.Api` and `Jobs.Worker` when configuration is required
- fixture(s) in `src/backend/tests/fixtures/`
- xUnit coverage in `src/backend/Jobs.Tests/Ingestion/`
- docs or sample updates under `docs/` when useful

## Constraints

- `IJobSource` returns `IAsyncEnumerable<JobPosting>` from `FetchAsync(...)`
- use the named `"Sources"` `HttpClient`
- use `System.Text.Json`
- use the injected `Fingerprint` service for dedupe data
- do not add EF migrations
- prefer the existing source families over new abstractions

## Final summary

At the end, summarize:
- which source pattern was chosen and why
- which files changed
- what validation was run
