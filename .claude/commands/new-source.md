---
argument-hint: <SourceName> <SourceUrl> <json|html>
description: Scaffold or wire a new ingestion source using the current patterns in this repository.
---

Use the current repository guidance from @CLAUDE.md, @docs/PROJECT_RULES.md, @docs/04_ingestion_sources.md, @src/backend/Jobs.Infrastructure/Ingestion/IJobSource.cs, @src/backend/Jobs.Infrastructure/Ingestion/IngestionPipeline.cs, @src/backend/Jobs.Infrastructure/DependencyInjection.cs, @src/backend/Jobs.Infrastructure/Options/AppOptions.cs, @src/backend/Jobs.Infrastructure/Ingestion/InfoJobsJobSource.cs, @src/backend/Jobs.Infrastructure/Ingestion/CorporateCareersJobSource.cs, @src/backend/Jobs.Infrastructure/Ingestion/GupyCompanyJobSource.cs, @src/backend/Jobs.Infrastructure/Ingestion/JsonLdJobSource.cs, @src/backend/Jobs.Worker/Worker.cs, and @src/backend/Jobs.Tests/Jobs.Tests.csproj.

Task: add a source with these inputs:

- Source name: `$1`
- Source URL: `$2`
- Response format: `$3`

If any required argument is missing, stop and tell me to run:

`/new-source <SourceName> <SourceUrl> <json|html>`

Before writing code, inspect the closest existing ingestion pattern and prefer reuse over new abstractions:

1. If the source is another `CorporateCareers`, `JsonLd`, or `GupyCompanies` site, extend the existing config-driven flow instead of creating a brand-new connector.
2. Only create a new `IJobSource` implementation when the fetch/parse behavior is materially different from the existing families.

Implement the full change set needed for a production-ready source:

- ingestion code and parser changes
- DI and/or options updates
- `appsettings*.json` updates in both `Jobs.Api` and `Jobs.Worker` when configuration is required
- fixture(s) in `src/backend/tests/fixtures/`
- xUnit coverage in `src/backend/Jobs.Tests/Ingestion/`
- sample payload or docs update under `docs/` when applicable

Constraints:

- `IJobSource` returns `IAsyncEnumerable<JobPosting>` from `FetchAsync(...)`
- use the named `"Sources"` `HttpClient`
- use `System.Text.Json`, not `Newtonsoft.Json`
- use the injected `Fingerprint` service for dedupe data
- keep ingestion deterministic and worker-friendly
- do not add EF migrations
- prefer the current codebase conventions over older prompt files if they conflict

At the end, summarize:

- which source pattern you chose and why
- which files changed
- what validation you ran
