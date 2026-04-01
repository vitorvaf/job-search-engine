---
paths:
  - "src/backend/**/*"
---

# Backend rules (.NET 8 / C#)

- Keep backend changes compatible with the current .NET 8 solution layout.
- Use `System.Text.Json` only; do not introduce `Newtonsoft.Json`.
- Keep dependency wiring in `src/backend/Jobs.Infrastructure/DependencyInjection.cs`.
- Bind configuration through strongly typed options in `src/backend/Jobs.Infrastructure/Options/`.
- Use `ILogger<T>` for logging; do not use `Console.WriteLine`.
- All outbound HTTP must use the named `"Sources"` `HttpClient` from `IHttpClientFactory`.
- Retry and backoff are already configured in infrastructure; avoid ad hoc transient-error retry loops.
- `IJobSource` implementations return `IAsyncEnumerable<JobPosting>` from `FetchAsync(IngestionFetchOptions options, CancellationToken ct)`.
- `ParsedSourceJob` is a parser/helper type; do not treat it as the final contract emitted by `IJobSource`.
- Build `JobPosting` using its nested value objects (`JobSourceRef`, `CompanyRef`, `LocationRef`, `SalaryRange`, `DedupeInfo`) instead of flat copies.
- Use the injected `Fingerprint` service when building dedupe metadata.
- `Jobs.Api` uses Minimal APIs in `Program.cs`; do not add MVC controllers or Razor Pages.
- The schema lives in `src/backend/Jobs.Infrastructure/Data/schema.sql`; do not add EF migrations.
- When changing model/schema/search behavior, update the domain model, entity layer, mapping layer, API shape, and Meilisearch indexing together.
- When changing fields that flow to search or filters, review `Jobs.Domain/Models/`, `Jobs.Infrastructure/Data/Entities/`, `Jobs.Infrastructure/Data/Mapping/MappingExtensions.cs`, `Jobs.Infrastructure/Search/MeiliClient.cs`, and `Jobs.Api/Program.cs` together.
- For new sources, first check whether they fit an existing config-driven pattern (`CorporateCareers`, `JsonLd`, `GupyCompanies`) before creating a brand-new connector.
- Reuse the existing parser families when possible: `InfoJobsHtmlParser`, `WorkdayJobsJsonParser`, `GupyJobsJsonParser`, `JsonLdHtmlParser`, and `TotvsHtmlParser`.
- `IngestionPipeline` is the source-of-truth orchestration for upsert, expiration, dedupe, and search indexing. Avoid duplicating pipeline logic in individual sources.
- If a source is configured via `App:Sources`, keep `appsettings.json` and `appsettings.Development.json` in both `Jobs.Api` and `Jobs.Worker` aligned.
