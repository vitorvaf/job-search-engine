## Summary

<!-- One paragraph describing WHAT changed and WHY. -->

## Type of change

- [ ] Bug fix (non-breaking)
- [ ] New feature (non-breaking)
- [ ] New job source / connector
- [ ] Breaking change (requires migration or consumer update)
- [ ] Refactor / tech debt
- [ ] Documentation / configuration only

---

## Checklist

### General
- [ ] Branch is up to date with `main` (or target branch)
- [ ] No secrets, credentials, or PII committed
- [ ] No `Console.WriteLine` added — logging goes through `ILogger<T>`

### Backend (if applicable)
- [ ] `dotnet build src/backend/Jobs.sln` passes with no warnings
- [ ] `dotnet test src/backend/Jobs.sln` passes
- [ ] No EF Migrations added (schema changes go in `schema.sql`)
- [ ] No `Newtonsoft.Json` added — project uses `System.Text.Json`
- [ ] New HTTP calls go through the named `"Sources"` `HttpClient` (never `new HttpClient()`)
- [ ] `JobPosting` domain model unchanged **OR** `JobPostingEntity`, `MappingExtensions`, and Meilisearch index updated

### New job source (if applicable)
- [ ] `IJobSource` implemented and registered in `DependencyInjection.cs`
- [ ] Sample fixture added to `src/backend/tests/fixtures/`
- [ ] Sample payload added to `docs/samples/`
- [ ] At least one xUnit test covering the happy-path parse
- [ ] Rate-limit respected via `IngestionFetchOptions.DelayBetweenRequestsMs`

### Frontend (if applicable)
- [ ] `npm run build` passes
- [ ] `npm run lint` passes with no errors
- [ ] No `pages/` directory created — App Router only
- [ ] Browser components do NOT call `BACKEND_URL` directly — BFF Route Handlers used
- [ ] Styling via Tailwind utility classes — no inline `style` attributes
- [ ] No new icon library added — only `lucide-react`

---

## Testing

<!-- Describe how this was tested (unit tests, manual steps, fixture used). -->

## Screenshots / recordings (if UI change)

<!-- Attach screenshots or a short video of the UI change. -->
