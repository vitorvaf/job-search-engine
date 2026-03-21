---
applyTo: "src/backend/**"
---

# Backend Coding Instructions (.NET 8 / C#)

These instructions apply to all files under `src/backend/`. Follow them strictly when generating or modifying C# code.

## Project & Solution Layout

```
Jobs.Domain/        Pure domain — no NuGet dependencies except the SDK
Jobs.Infrastructure/ EF Core, connectors (IJobSource), MeiliClient, IngestionPipeline
Jobs.Api/           ASP.NET Core Minimal API — route handlers in Program.cs only
Jobs.Worker/        BackgroundService orchestrating IJobSource instances
Jobs.Tests/         xUnit tests — fixtures at src/backend/tests/fixtures/
```

## Language & SDK Rules

- Target `net8.0` in every project.
- Enable `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` in every `.csproj`.
- Prefer `record` for immutable data transfer objects (e.g., `ParsedSourceJob`).
- Use `class` for EF Core entities and domain models.
- Use `sealed` on leaf classes to signal no intended inheritance.
- Prefer primary constructors (C# 12) for simple DI injection.
- Never use `var` for `dynamic` or when the type is non-obvious.

## Dependency Injection & Configuration

- Register services in `Jobs.Infrastructure/DependencyInjection.cs` — the single wiring point for infrastructure.
- Bind options with `services.Configure<TOptions>(config.GetSection("..."))`. Never inject `IConfiguration` directly into business logic.
- Use strongly-typed options (classes in `Jobs.Infrastructure/Options/`).
- `Fingerprint` is a singleton — always inject it; never compute SHA-256 inline.

## HTTP Clients

- ALL outbound HTTP must use the named `"Sources"` `HttpClient` from `IHttpClientFactory`. Never instantiate `HttpClient` directly.
- Polly retry/backoff is already wired in `DependencyInjection.cs`. Do not add manual `try/catch` for transient HTTP errors.
- Rate-limiting is controlled via `IngestionFetchOptions` — respect `DelayBetweenRequestsMs`.

## Database (EF Core + PostgreSQL)

- Use `JobsDbContext` via DI. Never create it manually.
- Schema is managed via `Jobs.Infrastructure/Data/schema.sql`. **Do NOT add EF Migrations** — just update `schema.sql` for schema changes and call `EnsureCreatedAsync()` at startup.
- Use `await db.Database.EnsureCreatedAsync()` on startup in both `Jobs.Api` and `Jobs.Worker`.
- Use `async`/`await` with `CancellationToken ct` in all EF queries.
- Prefer `ToListAsync`, `FirstOrDefaultAsync`, `AnyAsync` — never `.Result` or `.Wait()`.

## Logging

- Inject `ILogger<T>` via DI. Never use `Console.WriteLine`.
- Log at the correct level: `LogDebug` for trace info, `LogInformation` for milestones, `LogWarning` for recoverable errors, `LogError`/`LogCritical` for failures.

## Minimal API (Jobs.Api)

- All route handlers stay in `Program.cs`. No MVC controllers, no Razor Pages, no `[ApiController]`.
- Use `Results.Ok(...)`, `Results.NotFound()`, etc. from `Microsoft.AspNetCore.Http.Results`.
- Sanitise and clamp paging params (`page`, `pageSize`) before use.
- Escape Meilisearch filter values with the existing `EscapeFilterValue` helper.

## IJobSource Pattern — Adding a New Connector

1. Create `src/backend/Jobs.Infrastructure/Ingestion/<Name>JobSource.cs` implementing `IJobSource`.
2. `Name` property must return a human-readable source name (e.g., `"Gupy"`).
3. `Enabled` reads from strongly-typed options — never hardcode `true`.
4. Return `IAsyncEnumerable<ParsedSourceJob>` — do NOT change the interface signature.
5. Register in `DependencyInjection.cs` with `services.AddScoped<IJobSource, <Name>JobSource>()`.
6. Add fixture in `src/backend/tests/fixtures/` and a test in `Jobs.Tests/Ingestion/<Name>Tests.cs`.
7. Add sample payload in `docs/samples/sample_source_<name>.json` (or `.html`).

```csharp
// Template for a new IJobSource connector
public sealed class ExampleJobSource(
    IHttpClientFactory httpFactory,
    IOptions<AppOptions> options,
    ILogger<ExampleJobSource> logger) : IJobSource
{
    private readonly HttpClient _http = httpFactory.CreateClient("Sources");

    public string Name => "Example";
    public bool Enabled => options.Value.Sources.Example?.Enabled ?? false;

    public async IAsyncEnumerable<ParsedSourceJob> FetchJobsAsync(
        IngestionFetchOptions opts,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // fetch → parse → yield ParsedSourceJob
        yield break;
    }
}
```

## Third-Party Libraries

- Use `System.Text.Json` exclusively — **never** `Newtonsoft.Json`.
- Use `Polly` (already registered) for resilience — no other retry libraries.
- Do NOT add new NuGet packages for functionality already provided by the existing stack.
- Do NOT store secrets in `appsettings.json` — use environment variables or `src/backend/.env`.

## Async & Cancellation

- Every `async` method that performs I/O must accept `CancellationToken ct` as its last parameter.
- Propagate `ct` to every downstream call (`HttpClient`, EF Core, Task.Delay).
- Use `[EnumeratorCancellation]` on `CancellationToken` parameters inside `IAsyncEnumerable` methods.
